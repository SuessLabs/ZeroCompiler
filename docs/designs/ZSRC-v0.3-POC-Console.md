# Proof of Concept

In the next generation of Zero, we are re-aligning with the original design specifications and proof-of-concept.

```csharp
// Zero v0.2.0-pre
// Demonstrates high-level syntax

// Compiler options
option Platform = WinPE;  // Specify the target platform
option Arch     = x64;    // "Architecture" or "Arch" will work

// Library header inclusion
using "Windows.zh";

// Main entry point
int main()
{
  uint8 a = 1;
  word  b = 2;  // Word and uint8 are linked types (same thing)
  word  c = a + b;

  if (a > b)
    Console.Print()

  // Concatnation of strings and numeric values
  string msg = "Addition (a + b): " + c + "\n";
  Console.Print(msg);

  return 0;
}
