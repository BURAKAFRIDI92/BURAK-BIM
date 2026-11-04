BURAK BIM - install guide
=========================

INSTALL
  1. Close Revit, AutoCAD, Navisworks and your AI apps.
  2. Double-click BURAK-BIM-Installer.exe.
     No administrator rights, no internet and no .NET are needed.
  3. Fully quit and restart your AI app (Claude Desktop: quit from the tray icon).
  4. Open your BIM software and ask your AI app: "Use BURAK BIM: burak_status"

UNINSTALL
  Double-click BURAK-BIM-Uninstaller.exe.
  Your audit trail, reports, exports, config and logs are kept in the install folder.

IF WINDOWS SHOWS "WINDOWS PROTECTED YOUR PC"
  This appears when a release has no code-signing certificate.
  First compare the file with SHA256SUMS.txt:
      certutil -hashfile BURAK-BIM-Installer.exe SHA256
  Then click "More info" and "Run anyway".

SYSTEM
  Windows 10 or 11, 64-bit. There is no macOS version: Revit, Navisworks, ETABS,
  SAP2000, Bluebeam Revu and PlanSwift are Windows programs.

BLUEBEAM REVU
  Markups and measurements are read from the PDF, so save it in Revu first.
  Scripting needs Revu eXtreme or Complete.

PLANSWIFT
  Open PlanSwift and a job first. If the connection reports that the API is not
  registered, run PlanSwift once as administrator.

BURAK BIM TAB
  Revit, AutoCAD and AutoCAD-based products, and Navisworks show a BURAK BIM tab
  with an About section. The other supported programs connect without a tab.

CONTACT
  WhatsApp: +923189848904
  LinkedIn: Iftikhar Khan
  https://github.com/BURAKAFRIDI92/BURAK-BIM
