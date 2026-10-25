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
BrandingText "BURAK BIM - one AI connector for Autodesk software"
InstallDir "D:\BURAK BIM"
InstallDirRegKey HKCU "Software\BURAK BIM" "InstallDir"
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
!define MUI_WELCOMEPAGE_TEXT "BURAK BIM connects AI agents (Claude, ChatGPT Codex, Google Antigravity, Cursor, Gemini, VS Code...) to:$\r$\n$\r$\n  - Revit 2022-2027 (Architecture, Structure, Steel, MEP, Analyze, Manage, Add-Ins - every tab and command)$\r$\n  - AutoCAD, Civil 3D, Plant 3D, Map 3D, MEP, Advance Steel$\r$\n  - Navisworks Manage / Simulate (clash, TimeLiner)$\r$\n  - Inventor, ReCap Pro, Robot Structural Analysis$\r$\n  - Graphisoft Archicad, Rhino + Grasshopper, Dynamo$\r$\n$\r$\nSetup detects the installed versions, builds a bridge for each one and registers your AI clients. Internet access is needed once (.NET build tools).$\r$\n$\r$\nClose Revit, AutoCAD and Navisworks before continuing."
!define MUI_FINISHPAGE_TITLE "BURAK BIM is installed"
!define MUI_FINISHPAGE_TEXT "Restart your AI client (fully quit Claude Desktop from the tray), open Revit / AutoCAD / Navisworks and ask:$\r$\n$\r$\n   'Use BURAK BIM: burak_status'$\r$\n$\r$\nThe install log is in the logs folder."
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
Var Sentinel

Function .onInit
  ; D:\ when it exists, otherwise %LOCALAPPDATA%\BURAK BIM (the user may change it on the next page)
  ReadRegStr $0 HKCU "Software\BURAK BIM" "InstallDir"
  ${If} $0 != ""
    StrCpy $INSTDIR $0
  ${ElseIf} ${FileExists} "D:\*.*"
    StrCpy $INSTDIR "D:\BURAK BIM"
  ${Else}
    StrCpy $INSTDIR "$LOCALAPPDATA\BURAK BIM"
  ${EndIf}
  StrCpy $Clients "auto"
  StrCpy $Sentinel ""
FunctionEnd

Section "!BURAK BIM core (server + bridges for detected software)" SecCore
  SectionIn RO
  SetOutPath "$INSTDIR\setup"
  File /r /x bin /x obj "${PKG}\src"
  File /r "${PKG}\installer"
  File /nonfatal /r "${PKG}\prebuilt"
  File /nonfatal /r "${PKG}\docs"
  File /r "${PKG}\assets"
  File "${PKG}\README.md"
  File "${PKG}\AGENTS.md"
  File "${PKG}\LICENSE"
  File "${PKG}\CHANGELOG.md"
  CopyFiles /SILENT "$INSTDIR\setup\installer\install.ps1" "$INSTDIR\setup\install.ps1"
  CopyFiles /SILENT "$INSTDIR\setup\installer\uninstall.ps1" "$INSTDIR\setup\uninstall.ps1"
SectionEnd

Section "Connect AI clients found on this PC (Claude, Codex, Cursor, Antigravity...)" SecClients
SectionEnd

Section "Sentinel: auto-connect Autodesk versions installed later" SecSentinel
SectionEnd

Section "-Run installer"
  ${IfNot} ${SectionIsSelected} ${SecClients}
    StrCpy $Clients "none"
  ${EndIf}
  ${IfNot} ${SectionIsSelected} ${SecSentinel}
    StrCpy $Sentinel "-NoSentinel"
  ${EndIf}
  DetailPrint "Detecting Autodesk software and building BURAK BIM (this can take a few minutes the first time)..."
  nsExec::ExecToLog 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$INSTDIR\setup\install.ps1" -InstallDir "$INSTDIR" -Clients $Clients $Sentinel'
  Pop $0
  ${If} $0 == "2"
    DetailPrint "Installed with warnings - see the details above and $INSTDIR\logs."
  ${ElseIf} $0 != "0"
    DetailPrint "Installer returned $0 - see $INSTDIR\logs."
    MessageBox MB_ICONEXCLAMATION "BURAK BIM setup reported a problem (code $0).$\r$\nDetails: $INSTDIR\logs"
  ${EndIf}
  WriteUninstaller "$INSTDIR\Uninstall BURAK BIM.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\BURAK BIM" "UninstallString" '"$INSTDIR\Uninstall BURAK BIM.exe"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\BURAK BIM" "DisplayIcon" "$INSTDIR\setup\assets\burakbim.ico"
  CreateDirectory "$SMPROGRAMS\BURAK BIM"
  CreateShortcut "$SMPROGRAMS\BURAK BIM\BURAK BIM folder.lnk" "$INSTDIR"
  CreateShortcut "$SMPROGRAMS\BURAK BIM\BURAK BIM Doctor.lnk" "cmd.exe" '/k ""$INSTDIR\server\BurakBim.Server.exe" --doctor"' "$INSTDIR\setup\assets\burakbim.ico"
  CreateShortcut "$SMPROGRAMS\BURAK BIM\Reports.lnk" "$INSTDIR\reports"
  CreateShortcut "$SMPROGRAMS\BURAK BIM\Uninstall BURAK BIM.lnk" "$INSTDIR\Uninstall BURAK BIM.exe"
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecCore} "Server, bridges for every detected Revit / AutoCAD-family / Navisworks version, Inventor + ReCap support."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecClients} "Adds BURAK-BIM to Claude Desktop, Claude Code, Codex, Cursor, Windsurf, Google Antigravity, Gemini CLI and VS Code if they are installed (backups are kept)."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecSentinel} "A small logon task that connects newly installed Autodesk versions automatically."
!insertmacro MUI_FUNCTION_DESCRIPTION_END

Section "Uninstall"
  nsExec::ExecToLog 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$INSTDIR\setup\uninstall.ps1" -InstallDir "$INSTDIR" -Yes'
  Pop $0
  RMDir /r "$SMPROGRAMS\BURAK BIM"
  Delete "$INSTDIR\Uninstall BURAK BIM.exe"
  RMDir /r "$INSTDIR\setup"
  DetailPrint "Audit trail, reports, config and logs were kept in $INSTDIR."
SectionEnd
