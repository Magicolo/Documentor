using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Documentor
{
    sealed class Program
    {
        static void Main(string[] args)
        {
            var directory = Path.GetDirectoryName(typeof(Program).Assembly.Location);
            var references = Directory.EnumerateFiles(directory, "*.xml").Select(path => XElement.Load(path)).ToArray();
            var documentation = args.Select(path => (path, root: XElement.Load(path))).ToArray();
            var members = documentation.Select(pair => pair.root).Concat(references)
                .SelectMany(root => root.DescendantsAndSelf())
                .Where(node => node.Name == "member")
                .ToDictionary(node => node.Attribute("name").Value, child => child);
            foreach (var (path, root) in documentation) Process(path, root, members);
        }

        static void Process(string path, XElement root, Dictionary<string, XElement> members)
        {
            foreach (var node in root.DescendantsAndSelf())
            {
                if (node.Name == "inheritdoc" && node.Attribute("cref")?.Value is string cref && members.TryGetValue(cref, out var reference))
                {
                    var parent = node.Parent;
                    var children = node.ElementsBeforeSelf()
                        .Concat(reference.Elements())
                        .Concat(node.ElementsAfterSelf())
                        .ToArray();
                    parent.RemoveAll();
                    parent.Add(children);
                    RemoveDuplicates(parent);
                }
            }

            root.Save(path);
        }

        static void RemoveDuplicates(XElement parent)
        {
            foreach (var child in parent.Elements())
            {
                foreach (var sibling in child.ElementsAfterSelf())
                {
                    if (Corresponds(child, sibling))
                    {
                        child.Remove();
                        break;
                    }
                }
            }
        }

        static void Override(XElement[] sources, XElement[] targets)
        {
            foreach (var source in sources)
                if (targets.FirstOrDefault(node => Corresponds(source, node)) is XElement target) source.ReplaceWith(target);
        }

        static bool Corresponds(XElement source, XElement target)
        {
            if (source == target) return true;
            if (source == null || target == null) return false;
            return
                source.Name == target.Name &&
                source.Attributes().All(attribute => Corresponds(attribute, target.Attribute(attribute.Name)));
        }

        static bool Corresponds(XAttribute source, XAttribute target)
        {
            if (source == target) return true;
            if (source == null || target == null) return false;
            return source.Name == target.Name;
        }
    }
}
