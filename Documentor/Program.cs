using System;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Documentor
{
    class Program
    {
        static void Main(string[] args) => Parallel.ForEach(args, Process);

        static void Process(string path)
        {
            var root = XElement.Load(path);
            var descendants = root.DescendantsAndSelf().ToArray();
            var members = descendants
                .Where(node => node.Name == "member")
                .ToDictionary(node => node.Attribute("name").Value, child => child);

            foreach (var node in descendants)
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
