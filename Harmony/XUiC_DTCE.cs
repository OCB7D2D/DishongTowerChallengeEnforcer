class XUiC_DTCE : XUiController
{
    public static string ID = "";
    public static EntityPlayerLocal player;
    public override void Init()
    {
        base.Init();
        ID = WindowGroup.ID;
    }

    public override void OnOpen()
    {
        GameManager.Instance.Pause(true);
        player = GameManager.Instance.World.GetPrimaryPlayer();
        base.OnOpen();
        RefreshBindings();
    }

    public override void OnClose()
    {
        GameManager.Instance.Pause(false);
        base.OnClose();
    }

    public override bool GetBindingValue(ref string _value, string _bindingName)
    {
        switch (_bindingName)
        {
            case "died":
                _value = player.Died.ToString();
                return true;
            case "time":
                _value = GameManager.Instance.World.worldTime.ToString();
                return true;
            case "description":
                string format = Localization.Get("xuiDTCEFinished");
                _value = string.Format(
                    format != null ? format : "Finished in {0} with {1} death(s).",
                    GameManager.Instance?.World?.worldTime, player?.Died);
                return true;
            default:
                return base.GetBindingValue(ref _value, _bindingName);
        }
    }

}
