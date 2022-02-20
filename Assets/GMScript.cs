using System;
using System.IO;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = UnityEngine.Random;

// ReSharper disable once InconsistentNaming
[SuppressMessage("ReSharper", "InconsistentNaming")]
public class GMScript : MonoBehaviour
{
    public TileBase pieceTile;
    public TileBase emptyTile;
    public TileBase chunkTile;
    //public TileBase[] numberTiles;
    public Tilemap boardMap;
    public TMP_Text infoText;
    private int _score;
    private int _difficulty;
    private int _fixedUpdateFramesToWait = 10;
    private int _fixedUpdateCount;

    // My added information
    string logFile;
    int easyMode = 0;
    [SerializeField]int[] totalsCleared;
    int numCleared; // Number cleared in the current pack (1-4 lines unless you clear too fast)
    bool currClearing; // If currently in process of clearing lines

    // ReSharper disable once InconsistentNaming
    public bool DEBUG_MODE;
    private bool _dirty, _initialized;

    private int _minBx = 9999;
    private int _minBy = 9999;
    private int _maxBx = -9999;
    private int _maxBy = -9999;

    private int _inARow;

    // private int _width = 0, _height = 0;
    private readonly int BOUNDS_MAX = 20;
    private Vector3Int[] _myPiece;
    private Vector3Int[] _myChunk;
    
    
    private Vector3Int[] PIECE_T;
    private Vector3Int[] PIECE_L;
    private Vector3Int[] PIECE_Z;
    private Vector3Int[] PIECE_J;
    private Vector3Int[] PIECE_S;
    private Vector3Int[] PIECE_I;
    private Vector3Int[][] PIECES; 

    // ReSharper disable once InconsistentNaming
    private int wx2bx(int wx)
    {
        return wx - _minBx;
    }

    void InitializePieces()
    {
        PIECE_T = new Vector3Int[] { new(0,-1), new(1,-1), new(0,0),  new(-1,-1) };
        PIECE_L = new Vector3Int[] { new(0,-1), new(1,-1), new(1,0),  new(-1,-1) };
        PIECE_J = new Vector3Int[] { new(0,-1), new(1,-1), new(-1,0), new(-1,-1) };
        PIECE_S = new Vector3Int[] { new(0,-1), new(-1,-1),new(0,0),  new(1,0) };
        PIECE_Z = new Vector3Int[] { new(0,-1), new(1,-1), new(0,0),  new(-1,0) };
        PIECE_I = new Vector3Int[] { new(0,0),  new(-1,0), new(-2,0), new(1,0) };
        PIECES = new []{PIECE_T,PIECE_L,PIECE_Z,PIECE_J,PIECE_S,PIECE_I};
    }
    
    void Start()
    {
        _myPiece = null;
        _myChunk = null;
        _dirty = true;
        _initialized = false;
        InitializePieces();

        // Adaptivity lines to add store past few games info
        int[] singleClears = new int[5];
        int[] doubleClears = new int[5];
        int[] tripleClears = new int[5];
        int[] tetris = new int[5];
        int endAdded = 0;

        // Initializes numbers as -1 if note enough games played
        for(int i = 0; i < 5; i++) {
            singleClears[i] = -1;
            doubleClears[i] = -1;
            tripleClears[i] = -1;
            tetris[i] = -1;
        }

        // Checks the current data stored in the user log file
        logFile = Directory.GetCurrentDirectory() + "\\Assets\\user.log";
        totalsCleared = new int[4];
        totalsCleared = totalsCleared.Select(x => 0).ToArray();
        if(File.Exists(logFile)) {
            StreamReader reader = File.OpenText(logFile);
            string s = "";
            while((s = reader.ReadLine()) != null) {
                string[] words = s.Split(" ", 4);
                int wordNum = 0;
                foreach(string word in words) {
                    int val = Int32.Parse(word);
                    if(wordNum == 0) {
                        singleClears[endAdded] = val;
                    } else if(wordNum == 1) {
                        doubleClears[endAdded] = val;
                    } else if(wordNum == 2) {
                        tripleClears[endAdded] = val;
                    } else if(wordNum == 3) {
                        tetris[endAdded] = val;
                    } else { // More words than types of clears available
                        break;
                    }
                    wordNum++;
                    
                }
                endAdded = (endAdded + 1) % 5;
                Debug.Log($"Single:{words[0]}, Double:{words[1]}, Triple:{words[2]}, Tetris:{words[3]}");
            }
            reader.Close();

            // Determine if assistance needed
            int[] totals = new int[4];
            int numUsed = 0;
            for(int i = 0; i < tetris.Length; i++) {
                if(tetris[i] == -1) { // means run invalid
                    continue;
                } else {
                    numUsed++;
                    totals[0] += singleClears[i];
                    totals[1] += doubleClears[i];
                    totals[2] += tripleClears[i];
                    totals[3] += tetris[i];
                }
            }
            // Noramalizes information (for all but tetris)
            for(int i = 0; i < totals.Length - 1; i++) {
                if(totals[i] > 0) {
                    totals[i] = totals[i] / numUsed;
                }
            }

            if(numUsed == 0 || totals[3] > 0 || totals[0] + 2*totals[1] + 3*totals[2] >= 9) {
                // No assistance needed, so do nothing
            } else if(totals[2] > 0 || totals[0] + 2*totals[1] >= 4) {
                easyMode = 1; // Halfs difficulty increase rate
            } else {
                easyMode = 2; // 1/3 difficulty increase rate + no random blocks
            }

            Debug.Log("EasyMode: " + easyMode);

        } else {
            Debug.Log("File doesn't exists.");
        }
    }


    bool MakeNewPiece(int midX, int maxY)
    {
        if (null != _myPiece) 
            return false;
        var targetPiece = PIECES[Random.Range(0, PIECES.Length)];
        _myPiece = new Vector3Int[targetPiece.Length];
        for (var i = 0; i < targetPiece.Length; i++)
        {
            _myPiece[i].x = targetPiece[i].x + midX;
            _myPiece[i].y = targetPiece[i].y + maxY;
        }
        return ValidPiece();
    }
    
    void BlankBaseBoard()
    {
        for (int j = _minBy; j <= _maxBy; j++)
        for (int i = _minBx; i <= _maxBx; i++)
        {
            boardMap.SetTile(new Vector3Int(i,j,0),emptyTile);
        }
        MakeNewPiece(0,_maxBy);
    }

    void SetupBaseBoard()
    {
        // Find the bounds for the visible board
        _initialized = true;
        for (var wy = -1 * BOUNDS_MAX; wy < BOUNDS_MAX; wy++)
        for (var wx = -1 * BOUNDS_MAX; wx < BOUNDS_MAX; wx++)
        {
            var cTile = boardMap.GetTile(new Vector3Int(wx,wy,0));
            if (cTile)
            {
                if (wx < _minBx) _minBx = wx;
                if (wy < _minBy) _minBy = wy;
                if (wx > _maxBx) _maxBx = wx;
                if (wy > _maxBy) _maxBy = wy;
            }
        }

        BlankBaseBoard();
        Debug.Log($"BOARD SIZE = {(1 + _maxBx - _minBx)} x {(1 + _maxBy - _minBy)}");
    }

    bool KillRow(int row)
    {
        var newChunk = new Vector3Int[] { };
        foreach (var p in _myChunk)
        {
            if (p.y > row)
            {
                Vector3Int [] movedPieces = {new(p.x, p.y - 1, p.z)};
                newChunk = newChunk.Concat(movedPieces).ToArray();
            } else if (p.y < row)
            {
                Vector3Int [] movedPieces = {p};
                newChunk = newChunk.Concat(movedPieces).ToArray();
            }
        }
        _myChunk = newChunk;
        return true;
    }

    bool CheckKillChunk()
    {
        if (null == _myChunk) return false;
        for (var row = _minBy; row <= _maxBy; row++)
        {
            var maxCount = _maxBx - _minBx + 1; 
            foreach (var p in _myChunk)
            {
                if (p.y == row)
                {
                    maxCount--;
                }
            }

            if (0 == maxCount)
            { 
                _score += 1;
               if (DEBUG_MODE) Debug.Log($"KILL ROW: {row}! Score: {_score}");
               numCleared++;
               currClearing = true;
               return KillRow(row);
            }
        }

        return false;
    }
    
    private bool ValidWorldXY(int wx, int wy)
    {
        return (wx <= _maxBx && wx >= _minBx && wy <= _maxBy && wy >= _minBy);
    }

    private bool ValidMoveXY(int wx, int wy)
    {
        if (!ValidWorldXY(wx, wy))
            return false;
        return null == _myChunk || _myChunk.All(p => p.x != wx || p.y != wy);
    }

    private bool ValidPiece()
    {
        if (null == _myPiece) return false;
        return _myPiece.All(p => ValidMoveXY(p.x, p.y));
    }

    private bool ShiftPiece(int dx, int dy)
    {
        if (null == _myPiece) return false;
        foreach (var p in _myPiece)
        {
            if (!ValidMoveXY(p.x + dx, p.y + dy))
            {
                if (DEBUG_MODE) Debug.Log($"INVALID MOVE = {p.x + dx}, {p.y + dy}");
                return false;
            }
        }
        for (int i = 0; i < _myPiece.Length; i++)
        {
            _myPiece[i] = new Vector3Int(_myPiece[i].x + dx, _myPiece[i].y + dy);
        }

        _dirty = true;
        return true;
    }

    private bool RotatePiece()
    {
        // rotated_x = (current_y + origin_x - origin_y)
        // rotated_y = (origin_x + origin_y - current_x - ?max_length_in_any_direction)
        
        _dirty = true;
        if (null == _myPiece) return false;
        var newPiece = new Vector3Int[_myPiece.Length];
        Array.Copy(_myPiece,newPiece,_myPiece.Length);

        var origin = _myPiece[0];
        for (var i = 1; i < _myPiece.Length; i++ )
        {
            var rotatedX = _myPiece[i].y + origin.x - origin.y;
            var rotatedY = origin.x + origin.y - _myPiece[i].x;
            if (!ValidMoveXY(rotatedX, rotatedY))
                return false;
            newPiece[i] = new Vector3Int(rotatedX, rotatedY);
        }

        Array.Copy(newPiece, _myPiece, _myPiece.Length);
        return true;
    }

    Vector3Int RandomEnemyPoint()
    {
        return new Vector3Int(Random.Range(_minBx,_maxBx),Random.Range(_minBy,0));
    }
    bool AddChunkAtPoint(Vector3Int chunkPoint)
    {
        _myChunk ??= new Vector3Int[] {};
        if (_myChunk.Any(p => p.x == chunkPoint.x && p.y == chunkPoint.y))
            return false;
        _myChunk = _myChunk.Concat(new [] {chunkPoint}).ToArray();
        return true;
    }
    
    void ChunkPiece()
    {
        if (null == _myPiece) return;
        while (ShiftPiece(0, -1)) { }

        _myChunk ??= new Vector3Int[] {};
        _myChunk = _myChunk.Concat(_myPiece).ToArray();
        _myPiece = null;
    }
    
    void DoTetrisLeft()
    {
        ShiftPiece(-1,0);
    }

    void DoTetrisRight()
    {
        ShiftPiece(1,0);
    }
    
    void DoTetrisUp()
    {
        RotatePiece();
        // ShiftPiece(0,1);
    }

    void DoTetrisDown()
    {
        if (!ShiftPiece(0, -1))
        {
            ChunkPiece();
        }
    }

    void DoTetrisDrop()
    {
        ChunkPiece();
    }

    void DrawBoard()
    {
        for (int j = _minBy; j <= _maxBy; j++)
        for (int i = _minBx; i <= _maxBx; i++)
        {
            boardMap.SetTile(new Vector3Int(i,j,0),emptyTile);
        }

        if (null == _myChunk) return;
        foreach (var p in _myChunk)
        {
            boardMap.SetTile(p, chunkTile);
        }
    }

    void DrawPiece()
    {
        if (null == _myPiece) return;
        foreach (var p in _myPiece)
        {
            boardMap.SetTile(p,pieceTile);
        }
    }

    bool MakeRandomAngryChunk()
    {
        return AddChunkAtPoint(RandomEnemyPoint());
    }
    
    void FixedUpdate()
    {
        if (0 != _fixedUpdateCount++ % _fixedUpdateFramesToWait) return;
        DoTetrisDown();
        if (_inARow > _difficulty)
        {
            _difficulty = _inARow;
            if (_fixedUpdateFramesToWait > 2)
            {
                if(easyMode == 0) {
                    _fixedUpdateFramesToWait-= 2;
                } else if(easyMode == 1) {
                    _fixedUpdateFramesToWait-= 1;
                } // Else if easyMode == 2 do not increase speed
            }
        }

        if (CheckKillChunk())
        {
            _inARow++;
            if(easyMode != 2) {
                MakeRandomAngryChunk();
            }
        }
        else _inARow = 0;
        infoText.text = $"PTS:{_score}\n\nMAX:{_difficulty}\n\nCURRIC\n576";
        _fixedUpdateCount = 1;
        if(!currClearing && numCleared > 0) {
            if(numCleared == 1) {
                totalsCleared[0]++;
            } else if(numCleared == 2) {
                totalsCleared[1]++;
            } else if(numCleared == 3) {
                totalsCleared[2]++;
            } else {
                totalsCleared[3]++;
            }
            Debug.Log(numCleared);
            numCleared = 0;
        }
        currClearing = false;
    }
    
    void Update()
    {
        if (null == Camera.main) return; 
        if (!_initialized) SetupBaseBoard();
        if (null == _myPiece)
        {
            if (!MakeNewPiece(0,_maxBy))
            {   
                Debug.Log("NO VALID MOVE");
                // Writes to file the number of moves done
                StreamWriter writer = new(logFile, append: true);
                string s = "";
                for(int i = 0; i < 4; i++) {
                    s += totalsCleared[i] + " ";
                }

                writer.WriteLineAsync(s);
                writer.Close();
                Debug.Log($"Write Finished. Lines cleared: {totalsCleared[0]} {totalsCleared[1]} {totalsCleared[2]} {totalsCleared[3]}");
                Debug.Break();
            }
        }
        
        
        if (Input.GetKeyDown(KeyCode.Q)) { Debug.Break(); }
        else if (Input.GetMouseButtonDown(0)) 
        {
            var point = Camera.main.ScreenToWorldPoint(Input.mousePosition); 
            Vector3Int selectedTile = boardMap.WorldToCell(point);
            AddChunkAtPoint(selectedTile);
            // Debug.Log(selectedTile);
            // boardMap.SetTile(selectedTile, pieceTile); 
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) { DoTetrisLeft(); }
        else if (Input.GetKeyDown(KeyCode.RightArrow)) { DoTetrisRight(); }
        else if (Input.GetKeyDown(KeyCode.UpArrow)) { DoTetrisUp(); }
        else if (Input.GetKeyDown(KeyCode.DownArrow)) { DoTetrisDrop(); }

        if (_dirty)
        {
            DrawBoard();
            DrawPiece();
        }
    } 
    
   
}
