# SourceGeneratorToolKit  

Useful tools for generating code in C# and other languages.  

## Features  

### Design  

- SourceGeneratorToolkit is developed as a Source Generator itself.  
  - Necessary features are exposed with internal visibility within the target project.  
  - Compiler infrastructure types under `System.Runtime.CompilerServices` are also expanded when necessary.

### Classes  

- **`SourceBuilder`**: Formatted source code generator
- **`CodePart`**: Represents a part of the source code
- **`SourceStringHandler`**: String interpolation handler for source code generation

## License

Apache License Version 2.0
