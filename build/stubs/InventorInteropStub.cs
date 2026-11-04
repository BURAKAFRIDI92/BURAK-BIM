// Stand-in for Autodesk.Inventor.Interop, used only by CI to compile the Inventor add-in, because Autodesk's interop is not
// on NuGet. It proves that setup's compile step and our code work. It does NOT prove that these members match real Inventor:
// that needs a PC with Inventor installed. Never shipped.
namespace Inventor
{
    public interface ApplicationAddInSite { object Application { get; } }

    public interface ApplicationAddInServer
    {
        void Activate(ApplicationAddInSite addInSiteObject, bool firstTime);
        void Deactivate();
        void ExecuteCommand(int commandID);
        object Automation { get; }
    }

    public enum CommandTypesEnum { kQueryOnlyCmdType = 1 }
    public interface NameValueMap { }
    public delegate void ButtonDefinitionSink_OnExecuteEventHandler(NameValueMap Context);
    public interface ButtonDefinition { event ButtonDefinitionSink_OnExecuteEventHandler OnExecute; }
}
