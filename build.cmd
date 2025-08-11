@echo off

echo "--- Building for Windows x64 ---"
dotnet publish -c Release -r win-x64 --self-contained true
echo "--- Zipping for Windows x64 ---"
tar -a -c -f bin\SqlSchemaBridgeMCP-win-x64.zip -C bin\Release\net8.0\win-x64\publish .

echo "--- Building for Linux x64 ---"
dotnet publish -c Release -r linux-x64 --self-contained true
echo "--- Zipping for Linux x64 ---"
tar -a -c -f bin\SqlSchemaBridgeMCP-linux-x64.zip -C bin\Release\net8.0\linux-x64\publish .

echo "--- Building for macOS x64 ---"
dotnet publish -c Release -r osx-x64 --self-contained true
echo "--- Zipping for macOS x64 ---"
tar -a -c -f bin\SqlSchemaBridgeMCP-osx-x64.zip -C bin\Release\net8.0\osx-x64\publish .

echo "--- All builds completed and zipped. ---"
