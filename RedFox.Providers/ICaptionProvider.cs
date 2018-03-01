using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Provider
{
    public interface ICaptionProvider
    {
        void Send(string text);
    }
}
