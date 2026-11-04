; BURAK BIM - standalone uninstaller (NSIS 3), shipped next to the installer in the release ZIP.
; Finds the install folder from the registry and runs the uninstaller that belongs to the installed version,
; so it also removes older versions. Audit trail, reports, exports, config and logs are kept.
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
  !define OUTFILE "BURAK-BIM-Uninstaller.exe"
!endif

Name "BURAK BIM Uninstaller"
OutFile "${OUTFILE}"
Icon "${PKG}\assets\burakbim.ico"
BrandingText "BURAK BIM"
ShowInstDetails show
VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "BURAK BIM"
VIAddVersionKey "CompanyName" "BURAK BIM"
VIAddVersionKey "FileDescription" "BURAK BIM Uninstaller"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "LegalCopyright" "BURAK BIM"

!include "LogicLib.nsh"

Page instfiles

!define UNKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\BURAK BIM"

Function .onInit
  ReadRegStr $INSTDIR HKCU "Software\BURAK BIM" "InstallDir"
  ${If} $INSTDIR == ""
    ReadRegStr $INSTDIR HKCU "${UNKEY}" "InstallLocation"
  ${EndIf}
  ${If} $INSTDIR == ""
    MessageBox MB_ICONINFORMATION "BURAK BIM is not installed for this Windows user." /SD IDOK
    SetErrorLevel 2
    Quit
  ${EndIf}
  MessageBox MB_YESNO|MB_ICONQUESTION "Remove BURAK BIM from $INSTDIR?$\r$\n$\r$\nClose Revit, AutoCAD, Navisworks and your AI apps first.$\r$\nYour audit trail, reports, exports, config and logs are kept." /SD IDYES IDYES proceed
  SetErrorLevel 1
  Quit
  proceed:
FunctionEnd

Section "Remove BURAK BIM"
  ${If} ${FileExists} "$INSTDIR\Uninstall BURAK BIM.exe"
    DetailPrint "Running the installed uninstaller..."
    ; _?= keeps the uninstaller in place and makes this call wait for it; it must be the last argument, unquoted
    ExecWait '"$INSTDIR\Uninstall BURAK BIM.exe" /S _?=$INSTDIR' $0
    DetailPrint "Uninstaller returned $0"
    Delete "$INSTDIR\Uninstall BURAK BIM.exe"
  ${ElseIf} ${FileExists} "$INSTDIR\server\BurakBim.Server.exe"
    nsExec::ExecToLog '"$INSTDIR\server\BurakBim.Server.exe" --home "$INSTDIR" --uninstall'
    Pop $0
    RMDir /r "$INSTDIR\server"
    RMDir /r "$INSTDIR\setup"
  ${Else}
    DetailPrint "No uninstaller found in $INSTDIR - removing the registration only."
  ${EndIf}
  ; the logon task of versions before 1.3
  nsExec::Exec 'schtasks /Delete /TN "BURAK BIM Sentinel" /F'
  Pop $0
  DeleteRegKey HKCU "${UNKEY}"
  DeleteRegKey HKCU "Software\BURAK BIM"
  RMDir /r "$SMPROGRAMS\BURAK BIM"
  ; only removed when nothing was kept in it
  RMDir "$INSTDIR"
  DetailPrint "BURAK BIM was removed. Restart your AI apps. Anything kept is in $INSTDIR."
SectionEnd
