// =============================================================================
// Auto-generated-style wrapper for PlayerInputActions.inputactions
// Unity의 "Generate C# Class" 옵션과 동일한 역할.
// 프로젝트에서 .inputactions 파일의 "Generate C# Class"를 켜면 이 파일은 교체됩니다.
// =============================================================================

using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputActions : IInputActionCollection2, System.IDisposable
{
    public InputActionAsset asset { get; }

    public PlayerInputActions()
    {
        asset = InputActionAsset.FromJson(@"{
            ""name"": ""PlayerInputActions"",
            ""maps"": [
                {
                    ""name"": ""Gameplay"",
                    ""id"": ""a1b2c3d4-0001-4000-8000-000000000001"",
                    ""actions"": [
                        { ""name"": ""Move"",    ""type"": ""Value"",  ""id"": ""a1b2c3d4-0002-4000-8000-000000000001"", ""expectedControlType"": ""Vector2"", ""processors"": ""StickDeadzone"", ""interactions"": """", ""initialStateCheck"": true },
                        { ""name"": ""Attack"",  ""type"": ""Button"", ""id"": ""a1b2c3d4-0003-4000-8000-000000000001"", ""expectedControlType"": ""Button"",  ""processors"": """", ""interactions"": """", ""initialStateCheck"": false },
                        { ""name"": ""Dash"",    ""type"": ""Button"", ""id"": ""a1b2c3d4-0004-4000-8000-000000000001"", ""expectedControlType"": ""Button"",  ""processors"": """", ""interactions"": """", ""initialStateCheck"": false },
                        { ""name"": ""Skill1"",  ""type"": ""Button"", ""id"": ""a1b2c3d4-0005-4000-8000-000000000001"", ""expectedControlType"": ""Button"",  ""processors"": """", ""interactions"": """", ""initialStateCheck"": false },
                        { ""name"": ""Skill2"",  ""type"": ""Button"", ""id"": ""a1b2c3d4-0006-4000-8000-000000000001"", ""expectedControlType"": ""Button"",  ""processors"": """", ""interactions"": """", ""initialStateCheck"": false },
                        { ""name"": ""Interact"",""type"": ""Button"", ""id"": ""a1b2c3d4-0007-4000-8000-000000000001"", ""expectedControlType"": ""Button"",  ""processors"": """", ""interactions"": """", ""initialStateCheck"": false }
                    ],
                    ""bindings"": [
                        { ""name"": ""WASD"", ""id"": ""b1-01"", ""path"": """", ""action"": ""Move"", ""isComposite"": true, ""isPartOfComposite"": false },
                        { ""name"": ""up"",    ""id"": ""b1-02"", ""path"": ""<Keyboard>/w"", ""action"": ""Move"", ""isComposite"": false, ""isPartOfComposite"": true },
                        { ""name"": ""down"",  ""id"": ""b1-03"", ""path"": ""<Keyboard>/s"", ""action"": ""Move"", ""isComposite"": false, ""isPartOfComposite"": true },
                        { ""name"": ""left"",  ""id"": ""b1-04"", ""path"": ""<Keyboard>/a"", ""action"": ""Move"", ""isComposite"": false, ""isPartOfComposite"": true },
                        { ""name"": ""right"", ""id"": ""b1-05"", ""path"": ""<Keyboard>/d"", ""action"": ""Move"", ""isComposite"": false, ""isPartOfComposite"": true },
                        { ""name"": """", ""id"": ""b1-06"", ""path"": ""<Gamepad>/leftStick"", ""action"": ""Move"", ""isComposite"": false, ""isPartOfComposite"": false },
                        { ""name"": """", ""id"": ""b1-07"", ""path"": ""<Mouse>/leftButton"",  ""action"": ""Attack"", ""isComposite"": false, ""isPartOfComposite"": false },
                        { ""name"": """", ""id"": ""b1-08"", ""path"": ""<Gamepad>/buttonWest"", ""action"": ""Attack"", ""isComposite"": false, ""isPartOfComposite"": false },
                        { ""name"": """", ""id"": ""b1-09"", ""path"": ""<Keyboard>/space"",     ""action"": ""Dash"",   ""isComposite"": false, ""isPartOfComposite"": false },
                        { ""name"": """", ""id"": ""b1-10"", ""path"": ""<Gamepad>/buttonSouth"", ""action"": ""Dash"",   ""isComposite"": false, ""isPartOfComposite"": false },
                        { ""name"": """", ""id"": ""b1-11"", ""path"": ""<Keyboard>/q"",          ""action"": ""Skill1"", ""isComposite"": false, ""isPartOfComposite"": false },
                        { ""name"": """", ""id"": ""b1-12"", ""path"": ""<Gamepad>/buttonNorth"",  ""action"": ""Skill1"", ""isComposite"": false, ""isPartOfComposite"": false },
                        { ""name"": """", ""id"": ""b1-13"", ""path"": ""<Keyboard>/e"",           ""action"": ""Skill2"", ""isComposite"": false, ""isPartOfComposite"": false },
                        { ""name"": """", ""id"": ""b1-14"", ""path"": ""<Gamepad>/buttonEast"",   ""action"": ""Skill2"", ""isComposite"": false, ""isPartOfComposite"": false },
                        { ""name"": """", ""id"": ""b1-15"", ""path"": ""<Keyboard>/f"",           ""action"": ""Interact"", ""isComposite"": false, ""isPartOfComposite"": false }
                    ]
                },
                {
                    ""name"": ""UI"",
                    ""id"": ""a1b2c3d4-0008-4000-8000-000000000001"",
                    ""actions"": [
                        { ""name"": ""Pause"", ""type"": ""Button"", ""id"": ""a1b2c3d4-0009-4000-8000-000000000001"", ""expectedControlType"": ""Button"", ""processors"": """", ""interactions"": """", ""initialStateCheck"": false }
                    ],
                    ""bindings"": [
                        { ""name"": """", ""id"": ""b1-16"", ""path"": ""<Keyboard>/escape"",  ""action"": ""Pause"", ""isComposite"": false, ""isPartOfComposite"": false },
                        { ""name"": """", ""id"": ""b1-17"", ""path"": ""<Gamepad>/start"",    ""action"": ""Pause"", ""isComposite"": false, ""isPartOfComposite"": false }
                    ]
                }
            ],
            ""controlSchemes"": [
                { ""name"": ""Keyboard"", ""bindingGroup"": ""Keyboard"", ""devices"": [{ ""devicePath"": ""<Keyboard>"", ""isOptional"": false }, { ""devicePath"": ""<Mouse>"", ""isOptional"": true }] },
                { ""name"": ""Gamepad"",  ""bindingGroup"": ""Gamepad"",  ""devices"": [{ ""devicePath"": ""<Gamepad>"",  ""isOptional"": false }] },
                { ""name"": ""Touch"",    ""bindingGroup"": ""Touch"",    ""devices"": [{ ""devicePath"": ""<Touchscreen>"", ""isOptional"": false }] }
            ]
        }");

        _gameplay = new GameplayActions(this);
        _ui = new UIActions(this);
    }

    public void Dispose()
    {
        UnityEngine.Object.Destroy(asset);
    }

    // --- IInputActionCollection2 ---
    public InputBinding? bindingMask { get => asset.bindingMask; set => asset.bindingMask = value; }
    public System.Collections.Generic.IEnumerable<InputDevice>? devices { get => asset.devices; set => asset.devices = value; }
    public ReadOnlyArray<InputControlScheme> controlSchemes => asset.controlSchemes;
    public bool Contains(InputAction action) => asset.Contains(action);
    public System.Collections.Generic.IEnumerator<InputAction> GetEnumerator() => asset.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    public void Enable() => asset.Enable();
    public void Disable() => asset.Disable();
    public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false) => asset.FindAction(actionNameOrId, throwIfNotFound);
    public int FindBinding(InputBinding bindingMask, out InputAction action) => asset.FindBinding(bindingMask, out action);

    // =========================================================================
    // Gameplay Action Map
    // =========================================================================
    private readonly GameplayActions _gameplay;
    public GameplayActions Gameplay => _gameplay;

    public struct GameplayActions
    {
        private readonly PlayerInputActions _wrapper;

        public GameplayActions(PlayerInputActions wrapper) => _wrapper = wrapper;

        private InputActionMap GetMap() => _wrapper.asset.FindActionMap("Gameplay", true);

        public InputAction Move     => GetMap().FindAction("Move", true);
        public InputAction Attack   => GetMap().FindAction("Attack", true);
        public InputAction Dash     => GetMap().FindAction("Dash", true);
        public InputAction Skill1   => GetMap().FindAction("Skill1", true);
        public InputAction Skill2   => GetMap().FindAction("Skill2", true);
        public InputAction Interact => GetMap().FindAction("Interact", true);

        public void Enable()  => GetMap().Enable();
        public void Disable() => GetMap().Disable();
    }

    // =========================================================================
    // UI Action Map
    // =========================================================================
    private readonly UIActions _ui;
    public UIActions UI => _ui;

    public struct UIActions
    {
        private readonly PlayerInputActions _wrapper;

        public UIActions(PlayerInputActions wrapper) => _wrapper = wrapper;

        private InputActionMap GetMap() => _wrapper.asset.FindActionMap("UI", true);

        public InputAction Pause => GetMap().FindAction("Pause", true);

        public void Enable()  => GetMap().Enable();
        public void Disable() => GetMap().Disable();
    }
}
