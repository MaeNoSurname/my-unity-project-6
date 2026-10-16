using UnityEngine;
using UnityEngine.InputSystem;

public class ShipModeController : MonoBehaviour
{
    [SerializeField] private ShipMove shipMove;
    [SerializeField] private Shoot shoot;
    [SerializeField] private MouseRotate mouseRotate;

    [SerializeField] private Camera flyCamera;
    [SerializeField] private Camera shootCamera;

    [SerializeField] private bool startInShootMode = false;
    [SerializeField] private bool switchViewWithMode = true;

    private bool shootMode;
    private bool showingShootView;

    public bool IsShootMode => shootMode;

    private void Awake()
    {
        if (shipMove == null)
            shipMove = GetComponentInParent<ShipMove>();

        if (shipMove == null)
            shipMove = GetComponentInChildren<ShipMove>(true);

        if (shoot == null)
            shoot = GetComponentInChildren<Shoot>(true);

        if (mouseRotate == null)
            mouseRotate = GetComponentInChildren<MouseRotate>(true);
    }

    private void Start()
    {
        SetMode(startInShootMode);

        if (!switchViewWithMode)
        {
            showingShootView = false;
            SetView(false);
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (keyboard.tabKey.wasPressedThisFrame)
            ToggleMode();

        if (keyboard.vKey.wasPressedThisFrame)
            ToggleView();
    }

    public void SetMode(bool enableShootMode)
    {
        shootMode = enableShootMode;

        if (shipMove != null)
            shipMove.SetStraightFlightMode(shootMode);

        if (shoot != null)
            shoot.SetShootingEnabled(shootMode);

        if (mouseRotate != null)
            mouseRotate.SetAimEnabled(shootMode);

        if (switchViewWithMode)
        {
            showingShootView = shootMode;
            SetView(showingShootView);
        }

        Debug.Log(shootMode ? "Ship Mode: SHOOT" : "Ship Mode: FLY");
    }

    public void ToggleMode()
    {
        SetMode(!shootMode);
    }

    public void ToggleView()
    {
        if (flyCamera == null || shootCamera == null)
            return;

        showingShootView = !showingShootView;
        SetView(showingShootView);
    }

    private void SetView(bool showShootView)
    {
        if (flyCamera != null)
            flyCamera.enabled = !showShootView;

        if (shootCamera != null)
            shootCamera.enabled = showShootView;
    }
}