// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace Zero.Cli;

internal static class CliBanner
{
  /// <summary>The banner displayed during help and compilation; change this to select another style.</summary>
  public const string Default = AnsiShadow;

  /// <summary>The slogan displayed below the selected banner.</summary>
  public const string Tagline = ":: ZERO :: SOURCE > MACHINE ::";

  /// <summary>The bg banner style.</summary>
  public const string Bg = """
      _______ _ __ ___
     |_  / _ \ '__/ _ \
     / /  __/ | | (_) |
    /___\___|_|  \___/
    """;

  /// <summary>The Blurvision banner style.</summary>
  public const string Blurvision = """
    ░▒▓████████▓▒░▒▓████████▓▒░▒▓███████▓▒░ ░▒▓██████▓▒░
           ░▒▓█▓▒░▒▓█▓▒░      ░▒▓█▓▒░░▒▓█▓▒░▒▓█▓▒░░▒▓█▓▒░
         ░▒▓██▓▒░░▒▓█▓▒░      ░▒▓█▓▒░░▒▓█▓▒░▒▓█▓▒░░▒▓█▓▒░
       ░▒▓██▓▒░  ░▒▓██████▓▒░ ░▒▓███████▓▒░░▒▓█▓▒░░▒▓█▓▒░
     ░▒▓██▓▒░    ░▒▓█▓▒░      ░▒▓█▓▒░░▒▓█▓▒░▒▓█▓▒░░▒▓█▓▒░
    ░▒▓█▓▒░      ░▒▓█▓▒░      ░▒▓█▓▒░░▒▓█▓▒░▒▓█▓▒░░▒▓█▓▒░
    ░▒▓████████▓▒░▒▓████████▓▒░▒▓█▓▒░░▒▓█▓▒░░▒▓██████▓▒░
    """;

  /// <summary>The Ogre banner style.</summary>
  public const string Ogre = """
     _______ _ __ ___
    |_  / _ \ '__/ _ \
     / /  __/ | | (_) |
    /___\___|_|  \___/
    """;

  /// <summary>The ANSI Shadow banner style.</summary>
  public const string AnsiShadow = """
    ███████╗███████╗██████╗  ██████╗
    ╚══███╔╝██╔════╝██╔══██╗██╔═══██╗
      ███╔╝ █████╗  ██████╔╝██║   ██║
     ███╔╝  ██╔══╝  ██╔══██╗██║   ██║
    ███████╗███████╗██║  ██║╚██████╔╝
    ╚══════╝╚══════╝╚═╝  ╚═╝ ╚═════╝
    """;

  /// <summary>The AMC Slider banner style.</summary>
  public const string AmcSlider = """
                 ____                       ______
    `````````:' |            |`````````,  .~      ~.
          ..'   |______      |'''|'''''  |          |
      ..''      |            |    `.     |          |
    .:,,,,,,,,, |___________ |      `.    `.______.'
    """;

  /// <summary>The Shadow banner style.</summary>
  public const string Shadow = """
     _  /   _ \   __|  _ \
       /    __/  |    (   |
     ___| \___| _|   \___/
    """;

  /// <summary>Prints the default banner and slogan using UTF-8 for the Unicode artwork.</summary>
  public static void Write()
  {
    Console.OutputEncoding = new UTF8Encoding(false);
    Console.WriteLine(Default);
    Console.WriteLine(Tagline);
    Console.WriteLine();
  }
}
