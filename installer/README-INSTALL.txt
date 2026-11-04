BURAK BIM - install guide
=========================

INSTALL
  1. Double-click BURAK-BIM-Installer.exe. Setup finds your BIM software and AI apps.
     If Revit, AutoCAD or Navisworks is open, it asks you to save and close it.
     If Claude Desktop is open, setup offers to restart it.
     No administrator rights, no internet and no .NET are needed.
  2. Open your BIM software and ask your AI app: "Use BURAK BIM: burak_status"
     In Claude Desktop, BURAK BIM is listed under Settings > Developer.

  If you install an AI app later: Start menu > BURAK BIM > Connect AI apps again.

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
  with an About section. Inventor gets the same tab, built during setup.
  Rhino has no ribbon, so it gets a BURAK BIM panel (command: BurakBim).
  The other supported programs connect without a tab.

CONTACT
  WhatsApp: +923189848904
  LinkedIn: Iftikhar Khan
  https://github.com/BURAKAFRIDI92/BURAK-BIM
