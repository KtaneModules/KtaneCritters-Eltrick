using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KModkit;
using KeepCoding;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class CrittersScript : ModuleScript
{
    public KMSelectable SubmitButton, ResetButton, ReferenceTile;
    public GameObject ReferenceObject;
    public MeshRenderer ReferenceMesh, SubmitButtonMesh, ResetButtonMesh, ColourblindKey;
    public Material[] States, Alterations;
    public TextMesh ColourblindText;
    public AudioClip[] ButtonSounds;

    private KMBombModule _module;
    private KMSelectable[] _Tiles = new KMSelectable[64];
    private MeshRenderer[] _TileMeshes = new MeshRenderer[64];
    private IEnumerator _ButtonCoroutine;
    private System.Random _rnd;

    private bool[] _isTileHighlighted = new bool[64];
    private int[,] _isTileAlive = new int[8, 8];
    private int[,] _currentState = new int[8, 8];
    private int[][] _iterators = new int[3][]
    {
        new int[] {15, 14, 13, 03, 11, 05, 06, 01, 07, 09, 10, 02, 12, 04, 08, 00}, //normal
        new int[] {00, 01, 02, 12, 04, 10, 09, 14, 08, 06, 05, 13, 03, 11, 07, 15}, //alternative
        new int[] {15, 07, 11, 03, 13, 05, 06, 08, 14, 09, 10, 04, 12, 02, 01, 00}  //reverse
    };
    private string[] _shortenedColourNames = new string[3] { "Y", "P", "B" };
    private string[] _colourNames = new string[3] { "Yellow", "Pink", "Blue" };
    private string[] _alterationLogging = new string[3] { "using the standard ruleset", "using the alternative ruleset", "using the reverse ruleset" };
    private bool _isModuleSolved, _isSeedSet, _isGridGenerated, _isSubmitButtonHighlighted, _isAnimationRunning, _isResetButtonHighlighted, _isModuleBeingAutoSolved;
    private int _seed, _randomiser;
    private float[] _referenceCoordinate = new float[3];
    private string _grid;
    private string[] _submissionGrid = new string[64];
    private string[] _expectedGrid = new string[64];

    // Use this for initialization
    private void Start()
    {
        if(!_isSeedSet)
        {
            _seed = Rnd.Range(Int32.MinValue, Int32.MaxValue);
            Log("The seed is: " + _seed.ToString());
            _isSeedSet = true;
        }

        _rnd = new System.Random(_seed);
        // SET SEED ABOVE IN CASE OF BUGS!!
        // _rnd = new System.Random(loggedSeed);
        _module = Get<KMBombModule>();

        _referenceCoordinate[0] = ReferenceTile.transform.localPosition.x;
        _referenceCoordinate[1] = ReferenceTile.transform.localPosition.y;
        _referenceCoordinate[2] = ReferenceTile.transform.localPosition.z;

        for (int i = 0; i < 64; i++)
        {
            var x = i;
            _Tiles[i] = Instantiate(ReferenceTile, _module.transform);
            _TileMeshes[i] = _Tiles[i].GetComponentInChildren<MeshRenderer>();
            _module.GetComponent<KMSelectable>().Children[i] = _Tiles[i];
            _Tiles[i].Assign(onHighlight: () => { _isTileHighlighted[x] = true; });
            _Tiles[i].Assign(onHighlightEnded: () => { _isTileHighlighted[x] = false; });
            _Tiles[i].Assign(onInteract: () => { PressTile(x); });
        }

        SubmitButton.Assign(onHighlight: () => { _isSubmitButtonHighlighted = true; });
        SubmitButton.Assign(onHighlightEnded: () => { _isSubmitButtonHighlighted = false; });
        SubmitButton.Assign(onInteract: () => { PressSubmitButton(); });
        
        ResetButton.Assign(onHighlight: () => { _isResetButtonHighlighted = true; });
        ResetButton.Assign(onHighlightEnded: () => { _isResetButtonHighlighted = false; });
        ResetButton.Assign(onInteract: () => { PressResetButton(); });

        _module.GetComponent<KMSelectable>().UpdateChildren();

        if(!_isGridGenerated)
        {
            GenerateTiles();
            _isGridGenerated = true;
        }
        ReferenceObject.SetActive(false);
    }

    private void GenerateTiles()
    {
        _randomiser = _rnd.Next(0, 3);
        ColourblindText.text = _shortenedColourNames[_randomiser];
        switch(_randomiser)
        {
            case 0:
                ColourblindText.color = new Color32(255, 255, 128, 255);
                break;
            case 1:
                ColourblindText.color = new Color32(255, 128, 255, 255);
                break;
            case 2:
                ColourblindText.color = new Color32(128, 192, 255, 255);
                break;
        }
        Log("The colour shown on the module is " + _colourNames[_randomiser] + ", which indicates that we will be " + _alterationLogging[_randomiser] + ".");

        for (int i = 0; i < 64; i++)
        {
            int row = (int)Math.Floor((double)(i / 8));
            int column = i % 8;

            _Tiles[i].transform.localPosition = new Vector3(_referenceCoordinate[0] + (column % 8) * 0.0166f, _referenceCoordinate[1], _referenceCoordinate[2] - (row % 8) * 0.0166f);
            
            int gen = _rnd.Next(0, 2);
            switch (gen)
            {
                case 0:
                    _isTileAlive[row, column] = 0;
                    _submissionGrid[i] = "0";
                    _TileMeshes[i].material = States[0];
                    _Tiles[i].transform.localPosition -= new Vector3(0, 0.003f, 0);
                    break;
                case 1:
                    _isTileAlive[row, column] = 1;
                    _submissionGrid[i] = "1";
                    _TileMeshes[i].material = Alterations[_randomiser];
                    break;
            }
        }

        for (int tile = 0; tile < 64; tile++)
        {
            int row = (int)Math.Floor((double)(tile / 8));
            int column = tile % 8;

            if (_isTileAlive[row, column] == 1)
                _grid += "1";
            else
                _grid += "0";

            _expectedGrid[tile] = "0";
        }

        Log("The grid was: " + _grid);

        _currentState = _isTileAlive;

        for (int i = 0; i < 2; i++)
        {
            _currentState = IteratePartial(_currentState, _iterators[_randomiser], (i + _randomiser / 2) % 2);
            string current = "";
            for(int j = 0; j < 8; j++)
                for (int k = 0; k < 8; k++)
                    current += _currentState[j, k].ToString();
            Log("IP#" + (i + 1).ToString() + ": " + current);
        }

        string logMessage = "";

        for (int i = 0; i < 64; i++)
        {
            int row = (int)Math.Floor((double)(i / 8));
            int column = i % 8;

            _expectedGrid[i] = _currentState[row, column].ToString();
            logMessage += _expectedGrid[i];
        }
        Log("The expected grid is: " + logMessage);
    }

    private void PressTile(int index)
    {
        ButtonEffect(_Tiles[index], 0.5f, ButtonSounds[0]);
        if (_isModuleSolved || _isAnimationRunning)
            return;
        switch(_submissionGrid[index])
        {
            case "0":
                _submissionGrid[index] = "1";
                if(_ButtonCoroutine == null)
                {
                    _ButtonCoroutine = ButtonAnimation(index, "1");
                    StartCoroutine(_ButtonCoroutine);
                }
                break;
            case "1":
                _submissionGrid[index] = "0";
                if (_ButtonCoroutine == null)
                {
                    _ButtonCoroutine = ButtonAnimation(index, "0");
                    StartCoroutine(_ButtonCoroutine);
                }
                break;
        }
    }

    private int[,] IteratePartial(int[,] current, int[] iterator, int offset)
    {
        int[,] newGrid = new int[8, 8];
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
            {
                int state = 0;
                for (int k = 0; k < 4; k++)
                {
                    state *= 2;
                    state += current[(i * 2 + k % 2 + offset) % 8, (j * 2 + k / 2 + offset) % 8];
                }
                for (int k = 0; k < 4; k++)
                    newGrid[(i * 2 + k % 2 + offset) % 8, (j * 2 + k / 2 + offset) % 8] = (iterator[state] >> (3 - k)) % 2;
            }
        return newGrid;
    }

    private void PressSubmitButton()
    {
        if (_isModuleSolved || _isAnimationRunning)
            return;
        string log = "";
        string logExpected = "";

        for (int i = 0; i < 64; i++)
        {
            log += _submissionGrid[i];
            logExpected += _expectedGrid[i];
        }

        if (log != logExpected)
        {
            Strike("Submitted grid: " + log + ". Expected grid: " + logExpected + ".");
            for (int i = 0; i < 64; i++)
            {
                switch (_submissionGrid[i])
                {
                    case "0":
                        _TileMeshes[i].material = States[0];
                        break;
                    case "1":
                        _TileMeshes[i].material = Alterations[_randomiser];
                        _Tiles[i].transform.localPosition -= new Vector3(0, 0.003f, 0);
                        break;
                }
                _submissionGrid[i] = _grid[i].ToString();
                switch (_grid[i])
                {
                    case '1':
                        _TileMeshes[i].material = Alterations[_randomiser];
                        _Tiles[i].transform.localPosition += new Vector3(0, 0.003f, 0);
                        break;
                    default:
                        break;
                }
            }
        }
        else
        {
            Solve("Submitted correct grid.");
            ButtonEffect(SubmitButton, 1, ButtonSounds[1]);
            StartCoroutine(PostSolve());
            SubmitButton.transform.localPosition += new Vector3(0, 0.003f, 0);
            ResetButton.transform.localPosition += new Vector3(0, 0.003f, 0);
            _isModuleSolved = true;
            SubmitButtonMesh.material = Alterations[3];
            ColourblindText.text = "!";
            ColourblindText.color = new Color32(32, 32, 32, 255);
            ColourblindKey.material = Alterations[3];
        }
    }
    
    private void PressResetButton()
    {
        if (_isModuleSolved || _isAnimationRunning)
            return;
        for (int i = 0; i < 64; i++)
        {
            switch (_submissionGrid[i])
            {
                case "0":
                    _TileMeshes[i].material = States[0];
                    break;
                case "1":
                    _TileMeshes[i].material = Alterations[_randomiser];
                    _Tiles[i].transform.localPosition -= new Vector3(0, 0.003f, 0);
                    break;
            }
            _submissionGrid[i] = _grid[i].ToString();
            switch (_grid[i])
            {
                case '1':
                    _TileMeshes[i].material = Alterations[_randomiser];
                    _Tiles[i].transform.localPosition += new Vector3(0, 0.003f, 0);
                    break;
                default:
                    break;
            }
        }
    }

    private IEnumerator ButtonAnimation(int index, string state)
    {
        _isAnimationRunning = true;
        var originalLocation = _Tiles[index].transform.localPosition;
        if(state == "1")
        {
            for(int i = 0; i <= 3; i++)
            {
                _Tiles[index].transform.localPosition = new Vector3(originalLocation.x, originalLocation.y + i / 1000f, originalLocation.z);
                yield return null;
            }
            _TileMeshes[index].material = States[0];
        }
        else if(state == "0")
        {
            for (int i = 0; i <= 3; i++)
            {
                _Tiles[index].transform.localPosition = new Vector3(originalLocation.x, originalLocation.y - i / 1000f, originalLocation.z);
                yield return null;
            }
            _TileMeshes[index].material = Alterations[_randomiser];
        }
        _isAnimationRunning = false;
        _ButtonCoroutine = null;
    }

    private IEnumerator PostSolve()
    {
        yield return null;
        while(true)
        {
            for (int i = 0; i < 2; i++)
                _currentState = IteratePartial(_currentState, _iterators[_randomiser], (i + _randomiser / 2) % 2);
            for (int i = 0; i < 64; i++)
            {
                int row = (int)Math.Floor((double)(i / 8));
                int column = i % 8;

                switch (_currentState[row, column])
                {
                    case 0:
                        _Tiles[i].transform.localPosition = new Vector3(_Tiles[i].transform.localPosition.x, 0.0145f, _Tiles[i].transform.localPosition.z);
                        _TileMeshes[i].material = States[0];
                        break;
                    case 1:
                        _Tiles[i].transform.localPosition = new Vector3(_Tiles[i].transform.localPosition.x, 0.0175f, _Tiles[i].transform.localPosition.z);
                        _TileMeshes[i].material = Alterations[3];
                        break;
                }
            }
            yield return new WaitForSeconds(1f);
        }
    }

    private void FixedUpdate()
    {
        if (!_isModuleSolved)
        {
            for (int i = 0; i < 64; i++)
            {
                if (_isTileHighlighted[i])
                    _TileMeshes[i].material = States[2];
                else
                {
                    if (_submissionGrid[i] == "1")
                        _TileMeshes[i].material = Alterations[_randomiser];
                    else
                        _TileMeshes[i].material = States[0];
                }
            }
            if (_isSubmitButtonHighlighted)
                SubmitButtonMesh.material = States[2];
            else
                SubmitButtonMesh.material = States[0];
            if (_isResetButtonHighlighted)
                ResetButtonMesh.material = States[2];
            else
                ResetButtonMesh.material = States[0];
        }
    }

    // TP Support ?

#pragma warning disable 414
    private string TwitchHelpMessage = "'!{0} (a-h)(1-8)' to toggle the state of the tile at that position. '!{0} submit' or '!{0} s' to submit the current state. '!{0} reset' or '!{0} r' to revert the module to its initial state. All commands are chainable using spaces.";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string input)
    {
        if (_isModuleBeingAutoSolved)
            yield break;
        string[] split = input.ToLowerInvariant().Split(new[] { ' ', ';', ',' }, StringSplitOptions.RemoveEmptyEntries);

        Dictionary<string, KMSelectable> buttonNames = new Dictionary<string, KMSelectable>()
        {
            { "submit", SubmitButton },
            { "s", SubmitButton },
            { "reset", ResetButton },
            { "r", ResetButton }
        };

        List<KMSelectable> Tiles = new List<KMSelectable>();

        foreach (string button in split)
        {
            KMSelectable Tile;
            if (button.Length == 2)
            {
                int row = button[0] - 'a';
                int col = button[1] - '1';

                if (row < 0 || col < 0 || row > 7 || col > 7) yield return null;

                Tiles.Add(_Tiles[(8 * col) + row]);
            }
            else if (buttonNames.TryGetValue(button, out Tile))
                Tiles.Add(Tile);
        }

        if(Tiles.Count() == 0)
            yield return null;
        else
        {
            foreach (KMSelectable Tile in Tiles)
            {
                yield return null;
                Tile.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        _isModuleBeingAutoSolved = true;
        List<KMSelectable> TilesToPress = new List<KMSelectable>();
        Log("Module was force-solved by Twitch Plays.");

        for (int i = 0; i < 64; i++)
        {
            if (_submissionGrid[i] != _expectedGrid[i])
            {
                yield return null;
                TilesToPress.Add(_Tiles[i]);
            }
        }

        TilesToPress.Add(SubmitButton);
        
        foreach (KMSelectable Tile in TilesToPress)
        {
            yield return true;
            yield return new WaitForSeconds(0.1f);
            yield return null;
            Tile.OnInteract();
        }
    }
}
