using RedFox.Consumers;
using RedFox.Converters;
using RedFox.Messaging;
using RedFox.Notifications;
using RedFox.Providers;
using RedFox.Recorders;
using RedFox.Shared.Interfaces;
using RedFox.Transcribers;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace RedFox.Shared
{
    public enum Direction
    {
        /// <summary>
        /// Incoming (dial-in, push)
        /// </summary>
        In = 100,
        /// <summary>
        /// Outgoing (dial-up, pull)
        /// </summary>
        Out = 900
    }

    public enum State
    {
        /// <summary>
        /// The extension is initialized
        /// </summary>
        Idle,
        /// <summary>
        /// The extension is ready for use
        /// </summary>
        Ready,
        /// <summary>
        /// The extension is off-hook and listening
        /// </summary>
        Listening,
        /// <summary>
        /// The extension is in an active session
        /// </summary>
        Busy,
        /// <summary>
        /// The extension has ended the session
        /// </summary>
        Finished,
        /// <summary>
        /// The extension is stopped and can no longer be used
        /// </summary>
        Disposed
    }

    internal class Extensions
    {
        private static readonly Lazy<Extensions> lazy = new Lazy<Extensions>(() => new Extensions());

        public static Extensions Instance { get { return lazy.Value; } }

        private Extensions()
        { }

        [ImportMany(typeof(IConsumer), AllowRecomposition = true)]
        public IEnumerable<ExportFactory<IConsumer, IConsumerMetadata>> Consumers { get; set; }

        [ImportMany(typeof(IRecorder), AllowRecomposition = true)]
        public IEnumerable<Lazy<IRecorder, IExtensionMetadata>> Recorders { get; set; }

        [ImportMany(typeof(ITranscriber), AllowRecomposition = true)]
        public IEnumerable<Lazy<ITranscriber, IExtensionMetadata>> Transcribers { get; set; }

        [ImportMany(typeof(ICaptionConverter), AllowRecomposition = true)]
        public IEnumerable<Lazy<ICaptionConverter, IExtensionMetadata>> Converters { get; set; }

        [ImportMany(typeof(ICaptionProvider), AllowRecomposition = true)]
        public IEnumerable<Lazy<ICaptionProvider, IExtensionMetadata>> CaptionProviders { get; set; }

        [ImportMany(typeof(INotificationTarget), AllowRecomposition = true)]
        public IEnumerable<Lazy<INotificationTarget, INotificationMetadata>> NotificationTargets { get; set; }

        [Import(typeof(IMessageClient), AllowRecomposition = true)]
        public Lazy<IMessageClient, IExtensionMetadata> MessageClient { get; set; }
    }
}
