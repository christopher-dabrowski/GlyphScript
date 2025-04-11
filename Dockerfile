FROM mcr.microsoft.com/dotnet/sdk:9.0

RUN apt-get update && apt-get install -y llvm && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY GlyphScriptCompiler/*.csproj ./GlyphScriptCompiler/
COPY GlyphScriptCompiler.IntegrationTests/*.csproj ./GlyphScriptCompiler.IntegrationTests/
COPY *.slnx ./

RUN dotnet restore GlyphScriptCompiler/GlyphScriptCompiler.csproj

COPY GlyphScriptCompiler/ ./GlyphScriptCompiler/
COPY GlyphScript.g4 ./

RUN dotnet build GlyphScriptCompiler/GlyphScriptCompiler.csproj --configuration Release

VOLUME /source

WORKDIR /source

ENTRYPOINT ["dotnet", "run", "--project", "/app/GlyphScriptCompiler", "--"]
CMD ["--input", "program.gs", "--output", "program.ll"]
