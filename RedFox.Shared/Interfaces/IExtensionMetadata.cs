using System;
using System.ComponentModel.Composition;

namespace RedFox.Shared.Interfaces
{
    public interface IExtensionMetadata
    {
        string Name    { get; }
        string Version { get; }
    }

    [MetadataAttribute, AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ExtensionMetadataAttribute : ExportAttribute, IExtensionMetadata
    {
        public string    Name      { get; protected set; }
        public string    Version   { get; protected set; }

        public ExtensionMetadataAttribute(string name, string version) : base(typeof(IRedFox))
        {
            Name      = name;
            Version   = version;
        }
    }
}
