dotnet restore
dotnet build UndertaleRusInstallerGUI.Desktop
dotnet publish UndertaleRusInstallerGUI.Desktop -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true --output win_x86
dotnet publish UndertaleRusInstallerGUI.Desktop -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true --output "osx_x64\UndertaleRusInstaller.app\Contents\MacOS"
dotnet publish UndertaleRusInstallerGUI.Desktop -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true --output linux_x64

cd win_x86
del UndertaleRusInstaller.exe
rename UndertaleRusInstallerGUI.Desktop.exe UndertaleRusInstaller.exe
cd ..

cd "osx_x64\UndertaleRusInstaller.app\Contents\MacOS"
del UndertaleRusInstaller
rename UndertaleRusInstallerGUI.Desktop UndertaleRusInstaller
cd ..\..\..\..

cd linux_x64
del UndertaleRusInstaller
rename UndertaleRusInstallerGUI.Desktop UndertaleRusInstaller

pause