; BURAK BIM - Windows setup (NSIS 3). Build: makensis -DVERSION=1.0.0 -DPKG=<package dir> BurakBim.nsi
; Per-user install (no admin rights). Default folder D:\BURAK BIM so nothing large lands on C:.
Unicode true
SetCompressor /SOLID lzma
RequestExecutionLevel user
ManifestDPIAware true

!ifndef VERSION
  !define VERSION "1.0.0"
!endif
!ifndef PKG
  !define PKG ".."
!endif
!ifndef OUTFILE
  !define OUTFILE "BURAK-BIM-Setup-${VERSION}.exe"
!endif

Name "BURAK BIM ${VERSION}"
OutFile "${OUTFILE}"
BrandingText "BURAK BIM - one AI connector for BIM, CAD, structural and takeoff software"
VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "BURAK BIM"
VIAddVersionKey "CompanyName" "BURAK BIM"
VIAddVersionKey "FileDescription" "BURAK BIM Setup"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "LegalCopyright" "BURAK BIM"

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"

!define MUI_ICON "${PKG}\assets\burakbim.ico"
!define MUI_UNICON "${PKG}\assets\burakbim.ico"
!define MUI_WELCOMEFINISHPAGE_BITMAP "${PKG}\assets\wizard.bmp"
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_RIGHT
!define MUI_HEADERIMAGE_BITMAP "${PKG}\assets\header.bmp"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "Welcome to BURAK BIM ${VERSION}"
!define MUI_WELCOMEPAGE_TEXT "BURAK BIM connects AI agents (Claude, ChatGPT Codex, Google Antigravity, Cursor, Gemini, VS Code, Windsurf...) to:$\r$\n$\r$\n  - Revit 2022-2027, AutoCAD / Civil 3D / Plant 3D / Map 3D / Advance Steel 2022-2027, Navisworks 2022-2027$\r$\n  - SAP2000, ETABS, CSiBridge, SAFE$\r$\n  - Graphisoft Archicad, Bluebeam Revu, PlanSwift$\r$\n  - Inventor, ReCap Pro, Robot, Rhino + Grasshopper, Dynamo$\r$\n$\r$\nSetup detects the installed versions, installs a ready-made bridge for each one (no internet needed) and connects your AI apps.$\r$\n$\r$\nClose Revit, AutoCAD, Navisworks and your AI apps before continuing."
!define MUI_FINISHPAGE_TITLE "BURAK BIM is installed"
!define MUI_FINISHPAGE_TEXT "Restart your AI app (fully quit Claude Desktop from the tray icon), open your software and ask:$\r$\n$\r$\n   'Use BURAK BIM: burak_status'$\r$\n$\r$\nIf Claude Desktop does not show BURAK BIM, double-click BURAK-BIM.mcpb in the install folder.$\r$\nThe install log is in the logs folder."
!define MUI_FINISHPAGE_SHOWREADME "$INSTDIR\setup\README.md"
!define MUI_FINISHPAGE_SHOWREADME_TEXT "Open the README"
!define MUI_FINISHPAGE_SHOWREADME_NOTCHECKED
!define MUI_FINISHPAGE_LINK "Open the install folder"
!define MUI_FINISHPAGE_LINK_LOCATION "$INSTDIR"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "${PKG}\LICENSE"
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Var Clients

; $R0 program file, $R1 name to show. Retry checks again, Cancel carries on and setup reports what could not be done.
Function AskToClose
  retry:
    nsExec::ExecToStack 'cmd /c tasklist /NH /FI "IMAGENAME eq $R0" | "$SYSDIR\find.exe" /I "$R0"'
    Pop $0
    Pop $1
    StrCmp $0 "0" 0 done
    MessageBox MB_RETRYCANCEL|MB_ICONEXCLAMATION "Please close $R1, then click Retry.$\r$\n$\r$\nIf it stays open, BURAK BIM may not connect to it." /SD IDCANCEL IDRETRY retry
  done:
FunctionEnd

Function .onInit
  ; D:\ when it exists, otherwise %LOCALAPPDATA%\BURAK BIM (the user may change it on the next page, or pass /D=)
  ReadRegStr $0 HKCU "Software\BURAK BIM" "InstallDir"
  ${If} $INSTDIR != ""
    ; /D= on the command line wins
  ${ElseIf} $0 != ""
    StrCpy $INSTDIR $0
  ${ElseIf} ${FileExists} "D:\*.*"
    StrCpy $INSTDIR "D:\BURAK BIM"
  ${Else}
    StrCpy $INSTDIR "$LOCALAPPDATA\BURAK BIM"
  ${EndIf}
  StrCpy $Clients "auto"
FunctionEnd

; 1.3: plain files + one visible, signed-ready program (BurakBim.Server.exe --install). No PowerShell, no hidden windows,
; no scheduled tasks, no downloads, no admin rights - nothing that antivirus heuristics treat as suspicious.
Section "!BURAK BIM core (server, bridges for every detected version, skills)" SecCore
  SectionIn RO
  StrCpy $R0 "Claude.exe"
  StrCpy $R1 "Claude Desktop (right-click its tray icon and choose Quit)"
  Call AskToClose
  StrCpy $R0 "Revit.exe"
  StrCpy $R1 "Revit"
  Call AskToClose
  StrCpy $R0 "acad.exe"
  StrCpy $R1 "AutoCAD"
  Call AskToClose
  StrCpy $R0 "Roamer.exe"
  StrCpy $R1 "Navisworks"
  Call AskToClose
  ; stop a running server from an older version so its files can be replaced
  nsExec::Exec 'taskkill /F /IM BurakBim.Server.exe'
  Pop $0
  ; versions before 1.3 ran the server through dotnet.exe
  nsExec::Exec 'taskkill /F /FI "IMAGENAME eq dotnet.exe" /FI "MODULES eq BurakBim.Server.dll"'
  Pop $0
  SetOutPath "$INSTDIR\server"
  File /r "${PKG}\server\*"
  SetOutPath "$INSTDIR\setup"
  File /nonfatal /r "${PKG}\prebuilt"
  File /nonfatal /r "${PKG}\skills"
  File /nonfatal /r "${PKG}\tools"
  File /r /x bin /x obj "${PKG}\src"
  File /r "${PKG}\installer"
  File /nonfatal /r "${PKG}\docs"
  File /r "${PKG}\assets"
  File "${PKG}\README.md"
  File "${PKG}\AGENTS.md"
  File "${PKG}\LICENSE"
  File "${PKG}\CHANGELOG.md"
  SetOutPath "$INSTDIR"
  File /nonfatal "${PKG}\BURAK-BIM.mcpb"
SectionEnd

Section "Connect the AI apps found on this PC (Claude, Codex, Cursor, Antigravity, VS Code...)" SecClients
SectionEnd

Section "-Configure"
  ${IfNot} ${SectionIsSelected} ${SecClients}
    StrCpy $Clients "none"
  ${EndIf}
  DetailPrint "Detecting your software, installing bridges, connecting AI apps and skills..."
  nsExec::ExecToLog '"$INSTDIR\server\BurakBim.Server.exe" --home "$INSTDIR" --install --clients $Clients'
  Pop $0
  ${If} $0 != "0"
    DetailPrint "Setup step returned $0 - see $INSTDIR\logs."
    MessageBox MB_ICONEXCLAMATION "BURAK BIM could not finish the setup step (code $0).$\r$\nDetails: $INSTDIR\logs$\r$\nYou can run it again from Start menu > BURAK BIM > Repair / detect new software." /SD IDOK
  ${EndIf}
  WriteUninstaller "$INSTDIR\Uninstall BURAK BIM.exe"
  !define UNKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\BURAK BIM"
  WriteRegStr HKCU "${UNKEY}" "DisplayName" "BURAK BIM"
  WriteRegStr HKCU "${UNKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UNKEY}" "Publisher" "BURAK BIM"
  WriteRegStr HKCU "${UNKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNKEY}" "URLInfoAbout" "https://github.com/BURAKAFRIDI92/BURAK-BIM"
  WriteRegStr HKCU "${UNKEY}" "UninstallString" '"$INSTDIR\Uninstall BURAK BIM.exe"'
  WriteRegStr HKCU "${UNKEY}" "DisplayIcon" "$INSTDIR\setup\assets\burakbim.ico"
  WriteRegDWORD HKCU "${UNKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNKEY}" "NoRepair" 1
  CreateDirectory "$SMPROGRAMS\BURAK BIM"
  CreateShortcut "$SMPROGRAMS\BURAK BIM\BURAK BIM folder.lnk" "$INSTDIR"
  CreateShortcut "$SMPROGRAMS\BURAK BIM\BURAK BIM Doctor.lnk" "$SYSDIR\cmd.exe" '/k ""$INSTDIR\server\BurakBim.Server.exe" --doctor"' "$INSTDIR\setup\assets\burakbim.ico"
  CreateShortcut "$SMPROGRAMS\BURAK BIM\Repair - detect new software.lnk" "$SYSDIR\cmd.exe" '/k ""$INSTDIR\server\BurakBim.Server.exe" --home "$INSTDIR" --install"' "$INSTDIR\setup\assets\burakbim.ico"
  CreateShortcut "$SMPROGRAMS\BURAK BIM\Connect AI apps again.lnk" "$SYSDIR\cmd.exe" '/k ""$INSTDIR\server\BurakBim.Server.exe" --connect-clients auto"' "$INSTDIR\setup\assets\burakbim.ico"
  CreateShortcut "$SMPROGRAMS\BURAK BIM\Reports.lnk" "$INSTDIR\reports"
  ${If} ${FileExists} "$INSTDIR\BURAK-BIM.mcpb"
    CreateShortcut "$SMPROGRAMS\BURAK BIM\Add to Claude Desktop (extension).lnk" "$INSTDIR\BURAK-BIM.mcpb"
  ${EndIf}
  CreateShortcut "$SMPROGRAMS\BURAK BIM\Uninstall BURAK BIM.lnk" "$INSTDIR\Uninstall BURAK BIM.exe"
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecCore} "Server, ready-made bridges for every detected Revit / AutoCAD-family / Navisworks version (2022-2027), SAP2000 / ETABS / CSiBridge / SAFE, Archicad, Bluebeam, PlanSwift support and 27 BIM and VDC workflow skills."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecClients} "Adds BURAK-BIM to Claude Desktop, Claude Code, Codex, Cursor, Windsurf, Antigravity, Gemini CLI, VS Code, Visual Studio, Cline, Roo, LM Studio, Kiro and more, if installed (backups are kept)."
!insertmacro MUI_FUNCTION_DESCRIPTION_END

Section "Uninstall"
  nsExec::ExecToLog '"$INSTDIR\server\BurakBim.Server.exe" --home "$INSTDIR" --uninstall'
  Pop $0
  nsExec::Exec 'taskkill /F /IM BurakBim.Server.exe'
  Pop $0
  ; versions before 1.3 ran the server through dotnet.exe
  nsExec::Exec 'taskkill /F /FI "IMAGENAME eq dotnet.exe" /FI "MODULES eq BurakBim.Server.dll"'
  Pop $0
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\BURAK BIM"
  RMDir /r "$SMPROGRAMS\BURAK BIM"
  Delete "$INSTDIR\Uninstall BURAK BIM.exe"
  Delete "$INSTDIR\BURAK-BIM.mcpb"
  RMDir /r "$INSTDIR\server"
  RMDir /r "$INSTDIR\setup"
  DetailPrint "Audit trail, reports, config and logs were kept in $INSTDIR."
SectionEnd
