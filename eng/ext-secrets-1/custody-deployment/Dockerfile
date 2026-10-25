# Build context is produced offline by prepare-image.py from exact reviewed publish-output hashes.
FROM @@REVIEWED_RUNTIME_IMAGE@@
WORKDIR /app
COPY reviewed-publish/ ./
USER 1654:2001
ENTRYPOINT ["dotnet", "Hexalith.Platform.Custody.Dapr.dll"]
# No default serve/worker command, profile, native credentials, signer or enrollment is embedded.
