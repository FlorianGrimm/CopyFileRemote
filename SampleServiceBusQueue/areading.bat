echo a >%~dp0DataA\afile.txt
echo nota >%~dp0DataB\afile.txt
dotnet run "%~dp0..\CopyFileRemote.csproj" --ServiceType ServiceBusQueue --HostMe A --HostOther B --InputPath "%~dp0DataA"
