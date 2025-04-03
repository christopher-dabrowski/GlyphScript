SOURCE_FILE = program.gs
OUTPUT_FILE = program.ll

GRAMMAR_FILE = GlyphScript.g4
COMPILER_DIR = GlyphScriptCompiler/Antlr
NAMESPACE = GlyphScriptCompiler.Antlr

compile:
	dotnet run \
		--project ./GlyphScriptCompiler \
		--input $(SOURCE_FILE) \
		--output $(OUTPUT_FILE)

generateCompiler:
	antlr \
		-Dlanguage=CSharp \
		-visitor \
		-no-listener \
		-o $(COMPILER_DIR) \
		-Werror \
		$(GRAMMAR_FILE) \
		-package $(NAMESPACE)

test:
	dotnet test ./GlyphScriptCompiler.IntegrationTests
