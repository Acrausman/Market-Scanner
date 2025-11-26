[Setup]
AppName=MarketScanner
AppVersion=0.8
DefaultDirName={commonpf}\MarketScanner
OutputBaseFilename=MarketScannerSetup
Compression=lzma
SolidCompression=yes
AppId={{A6A1A1C3-8A04-4D7A-9E7B-F5A65CE1BD2F}}
AllowNoIcons=yes
SetupIconFile=MarketScanner.ico
[Files]
Source: "..\Publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
[Tasks]
Name: "desktopicon"; Description:"Create a &desktop shortcut"; GroupDescription: "Additional icons:"; Flags: checkedonce
Name: "runapp"; Description: "Run MarketScanner after installation"; Flags: unchecked
[Icons]
Name: "{group}\MarketScanner"; Filename: "{app}\MarketScanner.UI.Wpf.exe"; IconFilename: "{app}\MarketScanner.UI.Wpf.exe"
Name: "{commondesktop}\MarketScanner"; Filename: "{app}\MarketScanner.UI.Wpf.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\MarketScanner.UI.Wpf.exe"; Description: "Launch MarketScanner"; Tasks: runapp; Flags: nowait postinstall skipifsilent
[Code]
[Code]

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  UserChoice: Integer;
  SettingsPath: string;
begin
  if CurUninstallStep = usUninstall then
  begin
    SettingsPath := ExpandConstant('{userappdata}\MarketScanner\settings.json');
    if FileExists(SettingsPath) then
    begin
      UserChoice := MsgBox(
        'Do you want to delete your MarketScanner settings?' #13#10 +
        '(This includes saved filters, email settings, RSI method, etc.)',
        mbConfirmation, MB_YESNO or MB_DEFBUTTON2);

      if UserChoice = IDYES then
      begin
        if DeleteFile(SettingsPath) then
          MsgBox('Your settings have been deleted.', mbInformation, MB_OK)
        else
          MsgBox('Could not delete settings file.', mbError, MB_OK);
      end
      else
      begin
        MsgBox('Your settings were kept.', mbInformation, MB_OK);
      end;
    end
    else
    begin
      MsgBox('DEBUG: File DOES NOT EXIST at path above.', mbError, MB_OK);
    end;
  end;
end;
