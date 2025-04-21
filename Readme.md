# Glyph Script

[![Compiler Build](https://github.com/christopher-dabrowski/GlyphScript/actions/workflows/compiler-build.yml/badge.svg)](https://github.com/christopher-dabrowski/GlyphScript/actions/workflows/compiler-build.yml)
[![Integration Tests](https://github.com/christopher-dabrowski/GlyphScript/actions/workflows/integration-tests.yml/badge.svg)](https://github.com/christopher-dabrowski/GlyphScript/actions/workflows/integration-tests.yml)

Implementacja języka programowania ze składnią bazującą na emotikonach (glifach) :smile:
Projekt obejmuje wszystkie etapy przetwarzania kodu źródłowego, aż do utworzenia kodu maszynowego.

Przód kompilatora jest zrealizowany za pomocą narzędzia [ANTLR](https://www.antlr.org/).
Na podstawie drzewa AST jest generowana reprezentacja pośrednia zgodna ze specyfikacją LLVM.

Projekt jest wykonywany w ramach przedmiotu Języki formalne i kompilatory na Politechnice Warszawskiej.

## Etap 1: Proste operacje na zmiennych

### Wymagania minimalne

- [x] obsługa dwóch typów zmiennych: całkowite, rzeczywiste - [BasicNumberTypesTests.cs](GlyphScriptCompiler.IntegrationTests/BasicNumberTypesTests.cs)
- [x] podstawowa obsługa standardowego wejścia-wyjścia - [SimpleIoTests.cs](GlyphScriptCompiler.IntegrationTests/SimpleIoTests.cs)
- [x] obsługa podstawowych operacji artmetycznych - [ExpressionTests.cs](GlyphScriptCompiler.IntegrationTests/ExpressionTests.cs)
- [x] wskazywanie błędów podczas analizy leksykalno-składniowej - [SyntaxErrorTests.cs](GlyphScriptCompiler.IntegrationTests/SyntaxErrorTests.cs)

### Rozszerzenia

- [ ] obsługa zmiennych tablicowych
- [ ] obsługa macierzy liczb
- [ ] obsługa wartości logicznych
  - [ ] AND, OR, XOR, NEG
  - [ ] short-circuit boolean evaluation
- [x] obsługa liczb o różnej precyzji
- [ ] obsługa typu ciąg znaków

## Decyzje Architektoniczne

Kluczowe decyzje podjęte podczas implementacji.

### Wzorzec Visitor

#### Kontekst

ALNTR umożliwia generację szkieletu kompilatora na porstawie wzorca **Listener** lub **Visitor**.

### Decyzja

W projekcie zastosowano wzorzec projektowy **Visitor** do implementacji analizy semantycznej oraz generacji kodu **zamiast domyślnego podejścia**, którym jest Listener.
Dzięki temu możemy dokładnie decydować o sposobie przechodzenia drzewa AST oraz korzystać z szerszego kontekstu podczas generacji kodu LLVM.

### Wpływ

Przy generowaniu szkieletu kompilatora podawane są flagi `-visitor` oraz `-no-listener`, które wyłączają generację klasy Listener.
Widać to w pliku [Makefile](Makefile), w targecie _generateCompiler_.

Implementując kompilator w klasie [LlvmVisitor](GlyphScriptCompiler/LlvmVisitor.cs) bezpośrednio sterujemy przechodzeniem drzewa AST.
