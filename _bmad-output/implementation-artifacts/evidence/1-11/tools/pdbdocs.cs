using System.Reflection.Metadata;

// Lists the source documents recorded in a portable PDB.
using FileStream stream = File.OpenRead(args[0]);
using MetadataReaderProvider provider = MetadataReaderProvider.FromPortablePdbStream(stream);
MetadataReader reader = provider.GetMetadataReader();
foreach (DocumentHandle handle in reader.Documents)
{
    Console.WriteLine(reader.GetString(reader.GetDocument(handle).Name));
}
