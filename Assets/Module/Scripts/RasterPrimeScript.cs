using SysRnd = System.Random;
using System.Collections;
using UnityEngine;
using UnityRnd = UnityEngine.Random;
using KModkit;
using System.Threading;
using System.Linq;

public class RasterPrimeScript : MonoBehaviour
{
    bool TwitchPlaysActive;

    private KMBombModule _module;
    private KMAudio _audio;

    [SerializeField]
    private MeshRenderer _referenceTile;

    [SerializeField]
    private RasterButton _leftButton;
    private RasterButton _rightButton;

    private RasterPuzzle _puzzle = null;

    private string _inputs = "";

    private bool _solved = false;
    private bool _ready = false;


    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private readonly string _moduleName = "Raster Prime";


    public static readonly Color ActiveColor = new Color32(0x40, 0xE0, 0xE0, 0xFF);
    public static readonly Color InactiveColor = new Color32(0xE0, 0x40, 0xA0, 0xFF);

    private bool _focused = false;

    void Awake()
    {
        _moduleId = _moduleIdCounter++;
    }

    void Start()
    {
        _module = GetComponent<KMBombModule>();
        _audio = GetComponent<KMAudio>();

        int seed = UnityRnd.Range(int.MinValue, int.MaxValue);
        SysRnd random = new SysRnd(seed);
        Log("The seed is: " + seed.ToString());

        StartCoroutine(LoadPuzzle(random));
    }

    void Update()
    {
        if (!_focused || !_ready || TwitchPlaysActive)
            return;

        if (new KeyCode[] { KeyCode.LeftArrow, KeyCode.A }.Any(x => Input.GetKeyDown(x)))
            _leftButton.Selectable.OnInteract();

        if (new KeyCode[] { KeyCode.RightArrow, KeyCode.D }.Any(x => Input.GetKeyDown(x)))
            _rightButton.Selectable.OnInteract();
    }

    private static bool _isUsingThreads = false;
    private IEnumerator LoadPuzzle(SysRnd random)
    {
        //Wait an extra frame so TwitchPlaysActive can be set in TestHarness
        yield return null;

        KMSelectable moduleSelectable = GetComponent<KMSelectable>();

        if (!TwitchPlaysActive)
        {
            moduleSelectable.OnFocus = () =>
            {
                _focused = true;
                if (_ready)
                {
                    _leftButton.Enable();
                    _rightButton.Enable();
                }
            };
            moduleSelectable.OnDefocus = () =>
            {
                _focused = false;
                if (_ready)
                {
                    _leftButton.Disable();
                    _rightButton.Disable();
                }
            };
        }
        else
        {
            Debug.Log("TP is active");
            _focused = true;
        }

        Vector3 buttonScale = _leftButton.transform.localScale;
        _leftButton.transform.localScale *= 0.0001f;

        yield return new WaitForSecondsRealtime(UnityRnd.Range(0, 3f));

        //threads needed here

        yield return new WaitWhile(() => _isUsingThreads);
        _isUsingThreads = true;
        new Thread(() =>
        {
            _puzzle = RasterPuzzle.GeneratePuzzle(random);
        }).Start();
        yield return new WaitWhile(() => _puzzle == null);
        _isUsingThreads = false;

        //thread is done here

        StartCoroutine(_puzzle.InstantiateAllTiles(_referenceTile));

        Log("Generated shapes: [L:{0}, R:{1}].", _puzzle.LeftComponent, _puzzle.LeftComponent.Counterpart());
        Log("Generated puzzle: {0}.", _puzzle);
        Log("The solution is: {0}.", _puzzle.GetSolution());

        _leftButton.transform.localScale = buttonScale;
        _rightButton = Instantiate(_leftButton, _leftButton.transform.parent);
        _rightButton.transform.localPosition = new Vector3(-_leftButton.transform.localPosition.x, _leftButton.transform.localPosition.y, _leftButton.transform.localPosition.z);
        _leftButton.InstantiateTiles(_puzzle.LeftComponent);
        _leftButton.OnInteract = () =>
        {
            AddInput('L');
            Log("Pressed left.");
            _audio.PlaySoundAtTransform("ButtonPress", _leftButton.transform);
            StartCoroutine(_leftButton.InteractionAnimation(new Vector3(3, -3)));
        };
        _rightButton.InstantiateTiles(_puzzle.LeftComponent.Counterpart());
        _rightButton.OnInteract = () =>
        {
            AddInput('R');
            Log("Pressed right.");
            _audio.PlaySoundAtTransform("ButtonPress", _rightButton.transform);
            StartCoroutine(_rightButton.InteractionAnimation(new Vector3(-3, 3)));
        };
        if (_focused)
        {
            _leftButton.Enable();
            _rightButton.Enable();
        }
        moduleSelectable.Children = new KMSelectable[] { _leftButton.Selectable, _rightButton.Selectable };
        moduleSelectable.UpdateChildren();

        _ready = true;
    }

    private void AddInput(char input)
    {
        string targetInput = _puzzle.GetSolution().Replace(" ", "");
        _inputs += input;
        if (_inputs.Length > targetInput.Length)
            _inputs = _inputs.Substring(_inputs.Length - targetInput.Length);

        if (_inputs == targetInput)
            StartCoroutine(Solve());
    }

    private IEnumerator Solve()
    {
        _solved = true;
        StartCoroutine(_leftButton.PermanentDisable());
        StartCoroutine(_rightButton.PermanentDisable());
        yield return _puzzle.SolveAnimationInitial(_audio, transform);
        StartCoroutine(_puzzle.SolveAnimation());
        _audio.PlaySoundAtTransform("SolveTune", transform);
        _module.HandlePass();
    }

#pragma warning disable 414
    private string TwitchHelpMessage = "'!{0} press L RL' to press those directions.";
#pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        yield return null;
        if (!_ready)
        {
            yield return "sendtochaterror The module is not ready yet.";
            yield break;
        }

        command = command.ToLowerInvariant();
        string[] commands = command.Split(' ');
        if (commands.Length >= 1 && commands[0] == "press" && commands.Skip(1).All(x => x.All(y => "lr".Contains(y))))
        {
            foreach (string subcommand in commands.Skip(1))
                foreach (char action in subcommand)
                {
                    if (action == 'l')
                        _leftButton.Selectable.OnInteract();
                    if (action == 'r')
                        _rightButton.Selectable.OnInteract();
                    yield return new WaitForSeconds(0.1f);
                }
        }
        else
        {
            yield return "sendtochaterror Invalid command.";
            yield break;
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        while (!_ready)
            yield return true;
        while (!_solved)
        {
            string solution = _puzzle.GetSolution().Replace(" ", "");
            for (int i = 0; i < _inputs.Length + 1; i++)
            {
                string substring = _inputs.Substring(i);
                if (solution.StartsWith(substring))
                {
                    char action = solution[substring.Length];
                    if (action == 'L')
                        _leftButton.Selectable.OnInteract();
                    if (action == 'R')
                        _rightButton.Selectable.OnInteract();
                    yield return new WaitForSeconds(0.1f);

                    break;
                }
            }
        }
    }

    private void Log(string format, params object[] args)
    {
        Debug.LogFormat("[{0} #{1}] {2}", _moduleName, _moduleId, string.Format(format, args));
    }
}
