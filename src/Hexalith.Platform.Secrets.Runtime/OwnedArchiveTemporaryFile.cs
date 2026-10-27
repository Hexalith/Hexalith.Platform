namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Exclusive creation, retained descriptor and publication state prove temporary ownership. Existing candidates are never claimed.</summary>
public sealed class OwnedArchiveTemporaryFile:IDisposable
{
    private readonly HeldArchiveDirectory _directory;
    private readonly string _name;
    private bool _published,_disposed;
    /// <summary>The exclusively created unpublished stream. Its owner retains it through publication or cleanup.</summary>
    public FileStream Stream { get; }
    internal OwnedArchiveTemporaryFile(HeldArchiveDirectory directory,string name)
    { BoundedDurableFileOperation.CurrentToken.ThrowIfCancellationRequested();ArchiveVolumeFence.RequireCurrentDestination(directory.PathName);directory.VerifyLocator();_directory=directory;_name=name;Stream=new FileStream(directory.OpenFile(name,0x40|0x80|1),FileAccess.Write); }
    /// <summary>Publishes this exact newly created temporary while its participating storage fence is held.</summary>
    public void Publish(string final,bool replace=false)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        _directory.Publish(_name,final,replace);_published=true;
    }
    /// <inheritdoc />
    public void Dispose()
    {
        if(_disposed) return;_disposed=true;
        try { if(!_published) _directory.DeleteOwnedTemporary(_name,Stream.SafeFileHandle);_directory.Flush(); }
        finally { Stream.Dispose(); }
    }
}
