using RedFox.Shared;
using RedFox.Shared.Interfaces;

using System;
using System.ComponentModel.Composition;

namespace RedFox.Consumers
{
    public interface IConsumerMetadata : IExtensionMetadata
    {
        Direction Direction { get; }
    }

    [MetadataAttribute, AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ConsumerMetadataAttribute : ExtensionMetadataAttribute, IConsumerMetadata
    {
        public Direction Direction { get; private set; }

        public ConsumerMetadataAttribute(string name, string version, Direction direction) : base(name, version)
        {
            Direction = direction;
        }
    }
}
