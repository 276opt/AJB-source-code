using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
internal static class GameMetadata
{
    internal static void Inspect(string path)
    {
        using var file = File.OpenRead(path);
        using var pe = new PEReader(file);
        var metadata = pe.GetMetadataReader();
        foreach (var handle in metadata.TypeDefinitions)
        {
            var type = metadata.GetTypeDefinition(handle);
            string name = metadata.GetString(type.Name);
            if (name != "scrPlayer" && name != "scrHitErrorMeter" && name != "scrMarginTracker" && name != "ErrorMeterTick") continue;
            foreach (var methodHandle in type.GetMethods())
            {
                var method = metadata.GetMethodDefinition(methodHandle);
                string methodName = metadata.GetString(method.Name);
                if (!new[]{"Hit","Die","AddHit","DrawStraightTick","DrawCurvedTick","Show","Reset","RevertToLastCheckpoint"}.Contains(methodName)) continue;
                Console.WriteLine(name + "." + methodName + "(" + string.Join(", ", method.GetParameters().Select(p => metadata.GetString(metadata.GetParameter(p).Name))) + ")");
            }
        }
    }
}
