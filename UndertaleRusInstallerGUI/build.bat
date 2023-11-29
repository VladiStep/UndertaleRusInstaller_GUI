dotnet restore
dotnet build UndertaleRusInstallerGUI.Desktop
dotnet publish UndertaleRusInstallerGUI.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true --output win_x64
dotnet publish UndertaleRusInstallerGUI.Desktop -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true --output osx_x64
dotnet publish UndertaleRusInstallerGUI.Desktop -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true --output linux_x64
pause