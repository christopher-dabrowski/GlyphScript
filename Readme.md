# Glyph Script

[![Compiler Build](https://github.com/christopher-dabrowski/GlyphScript/actions/workflows/compiler-build.yml/badge.svg)](https://github.com/christopher-dabrowski/GlyphScript/actions/workflows/compiler-build.yml)
[![Integration Tests](https://github.com/christopher-dabrowski/GlyphScript/actions/workflows/integration-tests.yml/badge.svg)](https://github.com/christopher-dabrowski/GlyphScript/actions/workflows/integration-tests.yml)

Implementacja języka programowania ze składnią bazującą na emotikonach (glifach) :smile:
Projekt obejmuje wszystkie etapy przetwarzania kodu źródłowego, aż do utworzenia kodu maszynowego.

Przód kompilatora jest zrealizowany za pomocą narzędzia [ANTLR](https://www.antlr.org/).
Na podstawie drzewa AST jest generowana reprezentacja pośrednia zgodna ze specyfikacją LLVM.

Projekt jest wykonywany w ramach przedmiotu Języki formalne i kompilatory na Politechnice Warszawskiej.

## Etap 1: Proste operacje na zmiennych

### Podstawa

- [x] obsługa dwóch typów zmiennych: całkowite, rzeczywiste - [BasicNumberTypesTests.cs](GlyphScriptCompiler.IntegrationTests/BasicNumberTypesTests.cs)
- [x] podstawowa obsługa standardowego wejścia-wyjścia - [SimpleIoTests.cs](GlyphScriptCompiler.IntegrationTests/SimpleIoTests.cs)
- [x] obsługa podstawowych operacji artmetycznych - [ExpressionTests.cs](GlyphScriptCompiler.IntegrationTests/ExpressionTests.cs)
- [x] wskazywanie błędów podczas analizy leksykalno-składniowej - [SyntaxErrorTests.cs](GlyphScriptCompiler.IntegrationTests/SyntaxErrorTests.cs)

### Rozszerzenia

- [x] obsługa zmiennych tablicowych - [ArrayOperationsTests.cs](GlyphScriptCompiler.IntegrationTests/ArrayOperationsTests.cs)
- [] obsługa macierzy liczb
- [x] obsługa wartości logicznych
  - [x] AND, OR, XOR, NEG - [BoolOperationsTests.cs](GlyphScriptCompiler.IntegrationTests/BoolOperationsTests.cs)
  - [x] short-circuit boolean evaluation [BoolOperations.cs](GlyphScriptCompiler/TypeOperations/BoolOperations.cs)
- [x] obsługa liczb o różnej precyzji [DifferentPrecisionOperationsTests.cs](GlyphScriptCompiler.IntegrationTests/DifferentPrecisionOperationsTests.cs)
- [x] obsługa typu ciąg znaków [StringOperationsTests.cs](GlyphScriptCompiler.IntegrationTests/StringOperationsTests.cs)

## Etap 2: Sterowanie przepływem programu

### Podstawa

- [x] instrukcja warunkowe, pętla - [IfElseStatementTests](GlyphScriptCompiler.IntegrationTests/IfElseStatementTests.cs) [WhileLoopTests](GlyphScriptCompiler.IntegrationTests/WhileLoopTests.cs),
- [x] możliwość tworzenia funkcji - [FunctionTests](GlyphScriptCompiler.IntegrationTests/FunctionTests.cs),
- [x] obsługa zasięgu zmiennych (lokalne i globalne, w pełni funkcjonalne zmienne lokalne) - [VariableScopeTests](GlyphScriptCompiler.IntegrationTests/VariableScopeTests.cs)

### Rozszerzenia

- [ ] obsługa struktur
- [ ] obsługa klas
- [ ] dynamiczne typowanie
- [ ] funkcje-generatory

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

### Kolejność Wyrażeń Bez Poziomu Zasad Parsowania

#### Kontekst

Do uzyskania poprawnej kolejności wykonywania wyrażeń arytmetycznych potrzebne jest przypisanie priorytetów operatorom.
W przykładowym języku [LangX](https://github.com/sawickib/LangX/blob/main/realcalc/LangX.g4) z zadeklarowaniem dodatkowych zasad parsowania, by osiągnąć poprawną kolejność wykonywania wyrażeń arytmetycznych.

#### Decyzja

W projekcie zastosowano podejście bez dodatkowych zasad parsowania.
Współczesne wersje ANTLR umożliwiają obsługę operatorów o różnym priorytecie bez dodatkowych zasad parsowania, co zostało opisane w materiale [The ANTLR Mega Tutorial](https://tomassetti.me/antlr-mega-tutorial/#chapter52) w części _28. Dealing with Expressions_.

#### Wpływ

Nie jest potrzebne dodawanie dodatkowych zasad parsowania do gramatyki.
Kolejność operatorów jest ustalana na podstawie kolejności alternatyw w gramatyce.
Upraszcza to zdecydowanie czytelność [gramatyki GlyphScript](GlyphScript.g4).

### Modularyzacja Generacji Kodu LLVM

#### Kontekst

W trakcie implementacji generacji kodu LLVM dla różnych typów danych i operacji klasa LlvmVisitor zaczęła rozrastać się nadmiernie, co utrudniało jej utrzymanie.

#### Decyzja

W projekcie zastosowano podejście modułowe, dzieląc kod na podstawie typu danych. Dla każdego typu danych stworzono oddzielną klasę w folderze `TypeOperations`, implementującą interfejs `IOperationProvider`. Każda z tych klas odpowiada za generację kodu LLVM dla operacji specyficznych dla danego typu.

#### Wpływ

Rozwiązanie to pozwoliło znacząco ograniczyć rozrost głównej klasy LlvmVisitor oraz umożliwiło bardziej izolowany rozwój i testowanie funkcjonalności dla każdego typu danych. Dodatkowo, struktura ta ułatwia dodawanie nowych typów danych i operacji poprzez tworzenie nowych klas implementujących wspólny interfejs.
