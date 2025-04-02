GRAMMAR_FILE = GlyphScript.g4
COMPILER_DIR = GlyphScriptCompiler

SOURCE_FILE = program.gs
OUTPUT_FILE = program.ll

compile:
	dotnet run \
		--project ./GlyphScriptCompiler \
		$(SOURCE_FILE) \
		$(OUTPUT_FILE)

generateCompiler:
	antlr \
		-Dlanguage=CSharp \
		-visitor \
		-no-listener \
		-o $(COMPILER_DIR) \
		-Werror \
		$(GRAMMAR_FILE)

test:
	dotnet test ./GlyphScriptCompiler.IntegrationTests
