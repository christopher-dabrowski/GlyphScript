# Glyph Script

[![Compiler Build](https://github.com/christopher-dabrowski/GlyphScript/actions/workflows/compiler-build.yml/badge.svg)](https://github.com/christopher-dabrowski/GlyphScript/actions/workflows/compiler-build.yml)

Implementacja języka programowania ze składnią bazującą na emotikonach (glifach) :smile:
Projekt obejmuje wszystkie etapy przetwarzania kodu źródłowego, aż do utworzenia kodu maszynowego.

Przód kompilatora jest zrealizowany za pomocą narzędzia [ANTLR](https://www.antlr.org/).
Na podstawie drzewa AST jest generowana reprezentacja pośrednia zgodna ze specyfikacją LLVM.

Projekt jest wykonywany w ramach przedmiotu Języki formalne i kompilatory na Politechnice Warszawskiej.

## Etap 1: Proste operacje na zmiennych

### Wymagania minimalne

- [ ] obsługa dwóch typów zmiennych: całkowite, rzeczywiste,
- [ ] podstawowa obsługa standardowego wejścia-wyjścia
- [ ] obsługa podstawowych operacji artmetycznych,
- [ ] wskazywanie błędów podczas analizy leksykalno-składniowej

### Rozszerzenia

- [ ] obsługa zmiennych tablicowych
- [ ] obsługa macierzy liczb
- [ ] obsługa wartości logicznych
  - [ ] AND, OR, XOR, NEG
  - [ ] short-circuit boolean evaluation
- [ ] obsługa liczb o różnej precyzji
- [ ] obsługa typu ciąg znaków
