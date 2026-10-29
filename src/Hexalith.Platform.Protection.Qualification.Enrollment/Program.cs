using Hexalith.Platform.Protection;

Environment.ExitCode = args is ["--serve", var installation, var sha256]
    ? await ProtectionQualificationServerExecutable.RunAsync("enrollment", installation, sha256)
    : args.Length == 0 ? await ProtectionQualificationRoleProgram.RunAsync("enrollment", Path.Combine(AppContext.BaseDirectory, "protection-role.enrollment.json")) : 2;
