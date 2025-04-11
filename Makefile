SOURCE_FILE = program.gs
OUTPUT_FILE = program.ll

GRAMMAR_FILE = GlyphScript.g4
COMPILER_DIR = GlyphScriptCompiler/Antlr
NAMESPACE = GlyphScriptCompiler.Antlr
DOCKER_IMAGE_NAME = glyphscript-compiler
DOCKER_TEST_IMAGE_NAME = glyphscript-test
TEST_RESULTS_DIR = TestResults

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

dockerCompile:
	docker build -t $(DOCKER_IMAGE_NAME) .
	docker run --rm -v "$(PWD):/source" $(DOCKER_IMAGE_NAME) --input $(SOURCE_FILE) --output $(OUTPUT_FILE)

dockerTest:
	docker build -t $(DOCKER_TEST_IMAGE_NAME) -f Dockerfile.test .
	mkdir -p $(TEST_RESULTS_DIR)
	docker run --rm -v "$(PWD)/$(TEST_RESULTS_DIR):/app/$(TEST_RESULTS_DIR)" $(DOCKER_TEST_IMAGE_NAME)

clean:
	rm -rf $(OUTPUT_FILE)
	rm -rf $(TEST_RESULTS_DIR)
	dotnet clean
	docker rmi $(DOCKER_IMAGE_NAME) $(DOCKER_TEST_IMAGE_NAME) 2>/dev/null || true
