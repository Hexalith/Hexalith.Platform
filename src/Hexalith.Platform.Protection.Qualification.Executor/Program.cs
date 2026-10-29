using Hexalith.Platform.Protection;

Environment.ExitCode = args is ["--serve", var installation, var sha256]
    ? await ProtectionQualificationServerExecutable.RunAsync("executor", installation, sha256)
    : args.Length == 0 ? await ProtectionQualificationRoleProgram.RunAsync("executor", Path.Combine(AppContext.BaseDirectory, "protection-role.executor.json")) : 2;
