using Hexalith.Platform.Protection;

Environment.ExitCode = args is ["--serve", var installation, var sha256]
    ? await ProtectionQualificationServerExecutable.RunAsync("observer", installation, sha256)
    : args.Length == 0 ? await ProtectionQualificationRoleProgram.RunAsync("observer", Path.Combine(AppContext.BaseDirectory, "protection-role.observer.json")) : 2;
