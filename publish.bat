@echo off
echo Publishing WinUI_V3 in self-contained mode...

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=true

echo Publish completed. Check the bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/publish folder. 