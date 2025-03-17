cd linux_x64
"C:\Program Files\WinRAR\WinRAR.exe" a -r -afzip "..\UndertaleRusInstaller_Linux.zip" *
cd ..
zip_exec UndertaleRusInstaller_Linux.zip UndertaleRusInstaller

cd osx_x64
"C:\Program Files\WinRAR\WinRAR.exe" a -r -afzip "..\UndertaleRusInstaller_MacOS_unsigned.zip" *
cd ..
zip_exec UndertaleRusInstaller_MacOS_unsigned.zip "UndertaleRusInstaller.app/Contents/MacOS/UndertaleRusInstaller"

cd win_x86
"C:\Program Files\WinRAR\WinRAR.exe" a -r -afzip "..\UndertaleRusInstaller_Windows.zip" *
cd ..

pause