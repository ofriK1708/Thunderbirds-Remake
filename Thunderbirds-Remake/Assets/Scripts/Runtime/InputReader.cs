using UnityEngine;
using UnityEngine.InputSystem;

public class InputReader : MonoBehaviour
{
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference switchShipAction;

    public Vector2 MoveInput => moveAction.action.ReadValue<Vector2>();
    public bool SwitchPressed =>
        switchShipAction.action.WasPressedThisFrame();

    private void OnEnable()
    {
        moveAction.action.Enable();
        switchShipAction.action.Enable();
    }

    private void OnDisable()
    {
        moveAction.action.Disable();
        switchShipAction.action.Disable();
    }

    private void Update()
    {
        if (SwitchPressed)
        {
            Debug.Log("SwitchShip received");
        }
    }
}