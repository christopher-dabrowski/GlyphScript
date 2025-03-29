GRAMMAR_FILE = GlyphScript.g4
COMPILER_DIR = GlyphScriptCompiler

generateCompiler:
	antlr \
		-Dlanguage=CSharp \
		-visitor \
		-no-listener \
		-o $(COMPILER_DIR) \
		-Werror \
		$(GRAMMAR_FILE)
