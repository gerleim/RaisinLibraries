using System.IO;
using AvalonDock;
using Raisin.Core;
using AvalonDock.Layout.Serialization;

namespace Raisin.WPF.Base;

public static class DockLayoutHelper
{
    public static void SaveDockLayout(DockingManager manager, string xmlPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(xmlPath)!);
        var tmpXml = xmlPath + ".tmp";
        var serializer = new XmlLayoutSerializer(manager);
        serializer.Serialize(tmpXml);
        SafeFile.ReplaceOrMove(tmpXml, xmlPath);
    }

    public static bool RestoreDockLayout(DockingManager manager, Func<string, object?> contentResolver, string xmlPath)
    {
        if (!File.Exists(xmlPath))
            return false;

        var serializer = new XmlLayoutSerializer(manager);
        serializer.LayoutSerializationCallback += (_, args) =>
        {
            var content = contentResolver(args.Model.ContentId);
            if (content is not null)
                args.Content = content;
            else
                args.Cancel = true;
        };

        serializer.Deserialize(xmlPath);
        manager.Layout?.CollectGarbage();
        return true;
    }
}
