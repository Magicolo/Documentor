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
            var references = Load(Path.GetDirectoryName(typeof(Program).Assembly.Location));
            var documentation = args.SelectMany(Load).ToArray();
            var members = documentation.Concat(references)
                .SelectMany(pair => pair.root.DescendantsAndSelf())
                .Where(node => node?.Name == "member")
                .Select(node => (node, name: node?.Attribute("name")?.Value))
                .Where(pair => !string.IsNullOrWhiteSpace(pair.name))
                .ToDictionary(pair => pair.name, pair => pair.node);
            foreach (var (path, root) in documentation)
            {
                Console.WriteLine($"Processing '{path}'.");
                Process(path, root, members);
            }
        }

        static (string path, XElement root)[] Load(string path)
        {
            try
            {
                path = Path.GetFullPath(path.Trim('"'));
                if (File.Exists(path) && XElement.Load(path) is XElement element)
                    return new[] { (path, element) };

                if (Directory.Exists(path))
                    return Directory.EnumerateFiles(path, "*.xml", SearchOption.TopDirectoryOnly).SelectMany(Load).ToArray();
            }
            catch { }

            return Array.Empty<(string, XElement)>();
        }

        static void Process(string path, XElement root, Dictionary<string, XElement> members)
        {
            foreach (var node in root.DescendantsAndSelf().ToArray())
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
