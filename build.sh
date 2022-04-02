OUTPUT_DIR=./publish/

dotnet msbuild -t:BundleApp -p:RuntimeIdentifier=osx-x64 -p:Configuration=Release -p:OutDir=$OUTPUT_DIR
dotnet msbuild -t:BundleApp -p:RuntimeIdentifier=osx-arm64 -p:Configuration=Release -p:OutDir=$OUTPUT_DIR

dotnet publish -r win-x64 -c Release -o $OUTPUT_DIR
dotnet publish -r win-arm64 -c Release -o $OUTPUT_DIR

dotnet publish -r linux-x64 -c Release -o $OUTPUT_DIR
dotnet publish -r linux-arm64 -c Release -o $OUTPUT_DIR

