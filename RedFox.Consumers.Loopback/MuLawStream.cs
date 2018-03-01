using NAudio.Codecs;
using NAudio.Wave;

using System;
using static System.Int16;

// ReSharper disable once CheckNamespace
namespace RedFox.Consumers.Loopback
{
    /// <summary>
    /// Todo: Rename this class since it doesn't implement IStream
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    public class MuLawStream
    {
        private readonly bool _debug;

        public MuLawStream(bool debug = false)
        {
            _debug = debug;
            if (_debug)
            {
                _outputStream = new BufferedWaveProvider(WaveFormat.CreateMuLawFormat(8000, 1))
                {
                    // There is data loss if you change the buffer duration from the default.
                    // _sourceStream.BufferDuration = TimeSpan.FromMilliseconds(200),

                    DiscardOnBufferOverflow = true,

                    // If we don't set this to false we'll simply fill up disk space with zeros.
                    ReadFully = false
                };
            }
        }

        public IWaveProvider OutputStream
        {
            get
            {
                if (!_debug)
                {
                    throw new InvalidOperationException("Output stream can only be accessed in debug mode");
                }
                return _outputStream;
            }
        }

        public event DataHandler DataAvailable;
#pragma warning disable 414
        private bool _waitingForData = true;

        private readonly BufferedWaveProvider _outputStream;

        private const int SampleRateIn  = 48000;
        private const int SampleRateOut = 8000;

        private float[] _floats = new float[1000];

        public void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            _waitingForData = false;

            var outPos     = 0;
            var floatIndex = 0;

            try
            {
                CheckBuffers(e.BytesRecorded);

                // Note: Data ratio of stereo 32-bit 48KHz to mono 16-bit 8KHz is 24:1
                //var bytesReceived = e.BytesRecorded;
                //var outputSize    = bytesReceived / 24;
                //var outArray      = new byte[(int)Math.Ceiling(outputSize / 2d) * 2];

                // Note: Data ratio of stereo 32-bit 48KHz to mono 8-bit 8KHz is 48:1
                var bytesReceived = e.BytesRecorded;
                var outputSize    = bytesReceived / 48;
                var outArray      = new byte[(int)Math.Ceiling(outputSize / 2d) * 2];

                // 48KHz
                // 32Kbps
                //var i = 0;
                //var j = 0;
                //var k = 0;
                //var o = new float[e.BytesRecorded / 8];

                //while (i < e.BytesRecorded)
                //{
                //    var left  = BitConverter.ToSingle(e.Buffer, i);
                //    var right = BitConverter.ToSingle(e.Buffer, i + 4);
                //    var mono  = (left + right) * 0.5F;

                //    o[j] = mono;

                //    i = i + 8;
                //    j = j + 1;
                //}

                //var result = Downsample(o);

                //for (k = 0; k < result.Length; k++)
                //{
                //    outArray[k] = MuLawEncoder.LinearToMuLawSample((short) (result[k] * MaxValue)); 
                //}
                
                // #1 Resample to 8KHz
                var waveBuffer = Downsample(e);

                while (floatIndex < waveBuffer.FloatBufferCount)
                {
                    // #2 Convert to Mono
                    var leftSample  = waveBuffer.FloatBuffer[floatIndex++];
                    var rightSample = waveBuffer.FloatBuffer[floatIndex++];
                    var monoSample  = ConvertToMono(leftSample, rightSample);
                    
                    // #3 Convert to short and then mu-law
                    outArray[outPos++] = MuLawEncoder.LinearToMuLawSample((short)(monoSample * MaxValue));
                }

                if (DataAvailable != null)
                {
                    foreach (var delDelegate in DataAvailable.GetInvocationList())
                    {
                        delDelegate.DynamicInvoke(this, new DataEventArgs(outArray, outPos)); //24));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.WriteLine($"{nameof(outPos)}: {outPos}, {nameof(floatIndex)}: {floatIndex}");
                throw;
            }
            _waitingForData = true;
        }
        
        public static float ConvertToMono(float left, float right)
        {
            return left * .5f + right * .5f;
        }
        
        public static short ConvertToMono(short left, short right)
        {
            var outSample = left * .5f + right * .5f;
            // hard limiting
            if (outSample > MaxValue)
            {
                outSample = MaxValue;
            }
            if (outSample < MinValue)
            {
                outSample = MinValue;
            }
            return (short)outSample;
        }

        private float[] Downsample(float[] array)
        {
            var wrapper   = new ResamplerWrapper(1);
            var processed = wrapper.Engage(array, out float[] result, 48000, 16000);

            return result;
        }

        private WaveBuffer Downsample(WaveInEventArgs e)
        {
            Buffer.BlockCopy(e.Buffer, 0, _floats, 0, e.BytesRecorded);

            var resampler = new ResamplerWrapper(2);
            var samplesProcessed =
                resampler.Engage(_floats, e.BytesRecorded / 4, out var resampled, SampleRateIn, SampleRateOut);
            if (_debug)
            {
                Console.WriteLine($"processed {samplesProcessed} samples");
            }

            Buffer.BlockCopy(resampled, 0, _buffer, 0, resampled.Length * 4);

            return new WaveBuffer(_buffer) { FloatBufferCount = samplesProcessed };
        }

        private void CheckBuffers(int bytesReceived)
        {
            if (_floats.Length < bytesReceived * .33)
            {
                Array.Resize(ref _floats, (int)(bytesReceived * .5));
            }
            if (_buffer.Length < bytesReceived * 1.1)
            {
                Array.Resize(ref _buffer, (int)(bytesReceived * 1.5));
            }
        }

        private byte[] _buffer = new byte[10000];
    }
}