using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DubinsPathsTutorial;
using UtilsWithoutDirection;
using ImprovedAPF;

public class Controller : MonoBehaviour
{
    public bool work = false;
    public SettingControl setting;

    public float speed = 238.0f;

    [Range(-45.0f, 45.0f)]
    public float right = 0.0f;

    [Range(-45.0f, 45.0f)]
    public float up = 0.0f;

    public float upChangeSpped = 0.1f;
    public float rightChangeSpped = 0.1f;

    public float stand_Speed = 238.0f;
    public float stand_MaxRotate = 38.6f;
    public float stand_MaxCenterG = 0.8f;
    public float stand_HalfLength = 7225.0f;

    public float stand_OneRoundTime = 0.0f;
    public float stand_PerSecondRotate = 0.0f;
    public float stand_RotateCoefficient = 0.0f;

    public float right_coefficient = (float)((360 / ((2 * Mathf.PI * 7225) / 0.8)) / 38.6);

    public Animator animator;

    public Text text_x;
    public Text text_x_stand;
    public Text text_z;
    public Text text_z_stand;
    public Text text_h;
    public Text text_h_stand;
    public Text text_v;

    private float x_value;
    private float z_value;
    private float h_value;

    public Text text_r;
    public Image imagePoint;

    public bool limitFPS = false;

    public bool startSimulator = false;

    public bool navigate = false;
    public Vector3[] navigateRoute;
    public int nav_pointer = 1;
    public int frameCount = 0;
    public ShipWork enmyTarget;

    public GameObject model_RF;
    public bool work_RF = true;

    public LineRenderer path_Gone;

    public GameObject pathGiver;
    public GameObject pathTrace;

    public List<FindShip> findShips = new List<FindShip>();

    public float moveLength = 0.0f;
    public Text text_length;

    public bool execute_avoid_tatic = false;

    public bool turnStayRF = false;

    public Toggle set_turnStayRF;

    public FormationPredictor predictor = new FormationPredictor();

    public bool predicted_CV = false;

    public System.Numerics.Vector2 predicted_CV_pos = new System.Numerics.Vector2(0, -100);

    public bool early_stop = false;
    public int early_stop_level = 0;

    public int find_Ships_count = 0;
    public Vector3 ship_wait_to_solve = new Vector3(0, 0, 0);

    private void Awake()
    {
        if (limitFPS)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
        }
        if (set_turnStayRF != null)
        {
            set_turnStayRF.onValueChanged.AddListener(TurnStayRF);
        }
    }

    public void TurnStayRF(bool arg0)
    {
        turnStayRF = arg0;
    }

    // Start is called before the first frame update
    void Start()
    {
        stand_OneRoundTime = (2 * Mathf.PI * stand_HalfLength) / stand_Speed;
        stand_PerSecondRotate = 360 / stand_OneRoundTime;
        stand_RotateCoefficient = stand_PerSecondRotate / stand_MaxRotate;
    }

    public void StartSimulator()
    {
        if (startSimulator)
            return;
        startSimulator = true;
        path_Gone.SetPosition(0, this.transform.position);
        path_Gone.SetPosition(1, this.transform.position);
        StartCoroutine(PathRecord());
        RF_WORK();
        StartCoroutine(UpdateShip());
        if (navigate)
            StartCoroutine(SimulatorNavigate());
        else
            StartCoroutine(SimulatorMoving());
    }


    public void Work()
    {
        work = true;
    }

    public void End()
    {
        startSimulator = false;

        if (setting != null)
        {
            setting.Reset();
            enmyTarget = null;
            path_Gone.positionCount = 2;
            var defaultPoints = new Vector3[2];
            defaultPoints[0] = this.transform.position;
            defaultPoints[1] = this.transform.position;
            path_Gone.SetPositions(defaultPoints);
            findShips.Clear();
            work_RF = false;
            predicted_CV = false;
            predicted_CV_pos = new System.Numerics.Vector2(0, -100);
            early_stop = false;
            early_stop_level = 0;
            find_Ships_count = 0;
            execute_avoid_tatic = false;
            ship_wait_to_solve = new Vector3(0, 0, 0);

            if (pathTrace.transform.childCount == 0)
                return;

            for (int i = pathTrace.transform.childCount - 1; i > 0; i--)
                GameObject.Destroy(pathTrace.transform.GetChild(i).gameObject);

            StopCoroutine(PathRecord());
            StopCoroutine(RFWork());
            StopCoroutine(RFRest());
            StopCoroutine(UpdateShip());
            if (navigate)
                StopCoroutine(SimulatorNavigate());
            else
                StopCoroutine(SimulatorMoving());
        }
    }

    public void Broken()
    {
        if (startSimulator)
        {
            startSimulator = false;
            work_RF = false;
            speed = 0;
        }
    }

    IEnumerator SimulatorMoving()
    {
        while (startSimulator)
        {
            yield return null;

            if (enmyTarget != null)
            {
                float dist = Vector3.Distance(enmyTarget.transform.position, this.transform.position);
                // Debug.Log($"Dist to CV={dist}");
                if (dist < 15000.0f)
                {
                    right = 0.0f;
                    this.transform.LookAt(new Vector3(enmyTarget.transform.position.x, 10, enmyTarget.transform.position.z));
                    this.transform.Translate(0.0f, 0.0f, speed / 60.0f * Time.timeScale);
                }
                else
                {
                    this.transform.Translate(0.0f, 0.0f, speed / 60.0f * Time.timeScale);
                    this.transform.Rotate(0.0f, right * stand_RotateCoefficient / 60.0f * Time.timeScale, 0.0f);
                }
            }
            else
            {
                this.transform.Translate(0.0f, 0.0f, speed / 60.0f * Time.timeScale);
                this.transform.Rotate(0.0f, right * stand_RotateCoefficient / 60.0f * Time.timeScale, 0.0f);
            }
            moveLength += speed / 60.0f * Time.timeScale;
        }
    }

    IEnumerator SimulatorNavigate()
    {
        while (startSimulator)
        {
            yield return null;

            if (enmyTarget == null)
            {
                if (nav_pointer < navigateRoute.Length)
                {
                    this.transform.LookAt(navigateRoute[nav_pointer]);
                    this.transform.Translate(0.0f, 0.0f, speed / 60.0f * Time.timeScale);

                    frameCount += (int)(1 * Time.timeScale);

                    if (frameCount >= 3)
                    {
                        nav_pointer = nav_pointer + (frameCount / 3);
                        frameCount %= 3;
                    }
                }
                else
                    startSimulator = false;
            }
            else
            {
                this.transform.LookAt(new Vector3(enmyTarget.transform.position.x, 10, enmyTarget.transform.position.z));
                this.transform.Translate(0.0f, 0.0f, speed / 60.0f * Time.timeScale);
            }
        }
    }

    IEnumerator PathRecord()
    {
        while (startSimulator)
        {
            if (path_Gone.GetPosition(0) == path_Gone.GetPosition(1))
                path_Gone.SetPosition(1, this.transform.position);
            else
            {
                path_Gone.positionCount++;
                path_Gone.SetPosition(path_Gone.positionCount - 1, this.transform.position);
            }
            yield return new WaitForSeconds(0.05f);
        }
    }

    IEnumerator RFWork()
    {
        if ((startSimulator) && (work_RF))
        {
            model_RF.SetActive(true);

            yield return new WaitForSeconds(0.05f);
            StartCoroutine(RFRest());
        }
    }
    IEnumerator RFRest()
    {
        if (startSimulator)
        {
            model_RF.SetActive(false);
            yield return new WaitForSeconds(4.0f);
            StartCoroutine(RFWork());
        }
    }

    IEnumerator UpdateShip()
    {
        yield return new WaitForSeconds(0.1f);
        if (findShips.Count > 0)
        {
            for (int i = 0; i < findShips.Count; i++)
            {
                findShips[i].lostTime += 0.1f;
            }
        }
        if (startSimulator)
            StartCoroutine(UpdateShip());
    }

    public void RF_WORK()
    {
        work_RF = true;
        StartCoroutine(RFWork());
    }

    public void SettingAvoidPath(Vector3 ship_pos)
    {
        List<System.Numerics.Vector3> DetectedShips = new List<System.Numerics.Vector3>();
        System.Numerics.Vector3 detected_ship;
        for (int i = 0; i < findShips.Count; i++)
        {
            System.Numerics.Vector3 ship_position = new System.Numerics.Vector3(x: findShips[i].pos.x + findShips[i].lostTime * findShips[i].moveVec.x,
                                                                                y: 0.0f,
                                                                                z: findShips[i].pos.y + findShips[i].lostTime * findShips[i].moveVec.y) / 1000.0f;
            // if (findShips[i].lostTime == 0)
            // {
            //     detected_ship = ship_position;
            // }
            DetectedShips.Add(ship_position);
        }

        if (this.right != 0)
        {
            ship_wait_to_solve = ship_pos;
            execute_avoid_tatic = true;

        }
        else if (this.right == 0 && find_Ships_count != findShips.Count)
        // else if (this.right == 0)
        {
            string dubin_type = "";
            dubin_type = ModifyPath_surround_ship(ship_pos);
            // ModifyPath(ship_pos);

            find_Ships_count = findShips.Count;
            ship_wait_to_solve = new Vector3(0, 0, 0);
            execute_avoid_tatic = false;

            // ??????????????????
            // Vector2 self_2D_pos = new Vector2(this.transform.position.x, this.transform.position.z);
            System.Numerics.Vector3 startPos = new System.Numerics.Vector3(x: this.transform.position.x, y: this.transform.position.y, z: this.transform.position.z) / 1000.0f;

            // ??????????????????
            // Vector2 self_f = new Vector2(this.transform.forward.x, this.transform.forward.z);
            System.Numerics.Vector2 heading_vec = System.Numerics.Vector2.Normalize(new System.Numerics.Vector2(x: this.transform.forward.x, y: this.transform.forward.z));
            float startHeading = -(180 * Mathf.Atan2(this.transform.forward.z, this.transform.forward.x) / Mathf.PI - 90) * (Mathf.PI / 180);

            // Vector2 tar_2D_pos = new Vector2(ship_pos.x, ship_pos.z);

            float self_r = stand_HalfLength;
            float avoid_R = 23000 - self_r;

            #region #New tradegy
            List<PathGroup> pathGroups = GameObject.FindObjectOfType<PathGroupMaker>().pathGroups;
            List<System.Tuple<System.Numerics.Vector3, char>> InitialDiamondCircle = new List<System.Tuple<System.Numerics.Vector3, char>>();

            // ???pathGroups.Count > 1?????????????????????????????????????????????????????????????????????????????????
            if (pathGroups.Count > 1)
            {
                // ??????????????????????????????????????????
                for (int i = 0; i < pathGroups[1].Circles.Count; i++)
                {
                    // ?????????????????????????????????????????????(???????????????????????????)???????????????????????????????????????
                    if (i == pathGroups[1].Circles.Count - 1 && pathGroups[1].Circles[i].end != true)
                    {
                        // ????????????????????????????????????????????????
                        char turn_side = pathGroups[1].Circles[pathGroups[1].Circles.Count - 1].turnMode.ToString()[0];
                        // ?????????????????????????????????????????????InitialDiamondCircle???????????????????????????????????????????????????
                        System.Tuple<System.Numerics.Vector3, char> inital_circle = new System.Tuple<System.Numerics.Vector3, char>(
                                                                                    new System.Numerics.Vector3(x: pathGroups[1].Circles[pathGroups[1].Circles.Count - 1].transform.position.x,
                                                                                                                y: pathGroups[1].Circles[pathGroups[1].Circles.Count - 1].transform.position.y,
                                                                                                                z: pathGroups[1].Circles[pathGroups[1].Circles.Count - 1].transform.position.z) / 1000.0f,
                                                                                    turn_side);
                        InitialDiamondCircle.Add(inital_circle);

                    }
                    // ????????????????????????????????????????????????????????????
                    else
                    {
                        pathGroups[1].Circles[i].end = true;
                        pathGroups[1].Circles[i].gameObject.active = false;
                    }
                }
            }

            // ??????????????????????????????????????????????????????InitialDiamondCircle
            List<int> not_end_idx_list = new List<int>();
            for (int i = 0; i < pathGroups[0].Circles.Count; i++)
            {
                if (!pathGroups[0].Circles[i].end)
                {
                    char turn_side = pathGroups[0].Circles[i].turnMode.ToString()[0];
                    System.Tuple<System.Numerics.Vector3, char> inital_circle = new System.Tuple<System.Numerics.Vector3, char>(
                                                                                new System.Numerics.Vector3(x: pathGroups[0].Circles[i].transform.position.x,
                                                                                                            y: pathGroups[0].Circles[i].transform.position.y,
                                                                                                            z: pathGroups[0].Circles[i].transform.position.z) / 1000.0f,
                                                                                turn_side);
                    InitialDiamondCircle.Add(inital_circle);
                    not_end_idx_list.Add(i);
                }
            }

            // List<System.Numerics.Vector3> DetectedShips = new List<System.Numerics.Vector3>();
            // for (int i = 0; i < this.findShips.Count; i++)
            // {
            //     System.Numerics.Vector3 ship_position = new System.Numerics.Vector3(x: this.findShips[i].pos.x + this.findShips[i].lostTime * this.findShips[i].moveVec.x,
            //                                                                         y: 0.0f,
            //                                                                         z: this.findShips[i].pos.y + this.findShips[i].lostTime * this.findShips[i].moveVec.y) / 1000.0f;
            //     DetectedShips.Add(ship_position);
            // }

            // List<ShipPermutation> sp_candidates, sp_predictions;
            // if (DetectedShips.Count == 3)
            // {
            //     double course = -(180 * Mathf.Atan2(this.findShips[0].moveVec.y, this.findShips[0].moveVec.x) / Mathf.PI - 90);
            //     course = -course - 90;
            //     List<Ship> ships = new List<Ship>{
            //         new Ship(DetectedShips[0].X, DetectedShips[0].Z),
            //         new Ship(DetectedShips[1].X, DetectedShips[1].Z),
            //         new Ship(DetectedShips[2].X, DetectedShips[2].Z),
            //     };
            //     (sp_candidates, sp_predictions) = this.predictor.predict(new ShipPermutation(mode: "inference", ships: ships), current_course: course);

            //     Debug.Log($"Type={sp_predictions[0].formation}, CV=({sp_predictions[0].ship_position_predict["CVLL"].x}, {sp_predictions[0].ship_position_predict["CVLL"].y})");
            // }

            // avoid_path?????????????????????????????????????????????????????????insert??????0?????????
            (List<System.Tuple<MathFunction.Circle, char>> avoid_path, int push_circle_Index) = GeneratePath.GeneratePathFunc(startPos, startHeading, DetectedShips, InitialDiamondCircle, dubin_type);

            System.Numerics.Vector2 normal_vec;
            if (avoid_path[0].Item2 == 'R')
            {
                normal_vec = new System.Numerics.Vector2(heading_vec.Y, -heading_vec.X);
            }
            else
            {
                normal_vec = new System.Numerics.Vector2(-heading_vec.Y, heading_vec.X);
            }

            System.Drawing.PointF center = new System.Drawing.PointF(x: startPos.X + stand_HalfLength * normal_vec.X / 1000.0f,
                                                                    y: startPos.Z + stand_HalfLength * normal_vec.Y / 1000.0f);
            MathFunction.Circle first_avoid_circle = new MathFunction.Circle(center, stand_HalfLength / 1000.0f);

            // ??????????????????insert??????0?????????
            avoid_path.Insert(0, new System.Tuple<MathFunction.Circle, char>(first_avoid_circle, avoid_path[0].Item2));

            // ????????????????????????????????????(?????????????????????????????????????????????????????????????????????????????????)
            avoid_path.RemoveAt(1);

            // ?????????????????????????????????
            avoid_path = Reduce_Path(avoid_path);

            //??????push_circle_Index????????????????????????end?????????true
            for (int i = 0; i < InitialDiamondCircle.Count; i++)
            {
                if (push_circle_Index >= 0)
                {
                    if (i == 0)
                    {
                        if (pathGroups.Count > 1 && pathGroups[1].Circles[pathGroups[1].Circles.Count - 1].end != true)
                        {
                            pathGroups[1].Circles[pathGroups[1].Circles.Count - 1].end = true;
                            pathGroups[1].Circles[pathGroups[1].Circles.Count - 1].gameObject.active = false;
                        }
                        else
                        {
                            pathGroups[0].Circles[not_end_idx_list[0]].end = true;
                            pathGroups[0].Circles[not_end_idx_list[0]].gameObject.active = false;
                            not_end_idx_list.RemoveAt(0);
                        }

                    }
                    else
                    {
                        pathGroups[0].Circles[not_end_idx_list[0]].end = true;
                        pathGroups[0].Circles[not_end_idx_list[0]].gameObject.active = false;
                        not_end_idx_list.RemoveAt(0);

                    }
                    push_circle_Index -= 1;
                }
                else
                {
                    break;
                }
            }
            if (pathGroups.Count == 2)
            {
                pathGroups.RemoveAt(1);
            }

            var path = GameObject.FindObjectOfType<PathGroupMaker>();

            // ????????????????????????????????????????????????????????????
            var avoidPath = new PathSetting();
            avoidPath.name = "avoidPath1";
            for (int i = 0; i < avoid_path.Count; i++)
            {
                var C = new CircleData();
                C.position = new Vector2(x: avoid_path[i].Item1.center.X, y: avoid_path[i].Item1.center.Y) * 1000;
                if (avoid_path[i].Item2 == 'R')
                {
                    C.turnMode = TurnMode.Right;
                }
                else
                {
                    C.turnMode = TurnMode.Left;
                }

                // ???????????????????????????????????????????????????????????????
                avoidPath.circleDatas.Add(C);
            }

            // ???????????????????????????PathGroupMaker??????SettingPathGroup
            GameObject.FindObjectOfType<PathGroupMaker>().SettingPathGroup(avoidPath);

            #endregion
        }

    }

    public List<System.Tuple<MathFunction.Circle, char>> Reduce_Path(List<System.Tuple<MathFunction.Circle, char>> avoid_path)
    {
        // ??????????????????????????????????????????????????????
        for (int i = 1; i < avoid_path.Count - 1; i++)
        {
            // ???????????????????????????????????????????????????????????????
            float angle_first = (float)MathFunction.Angle(avoid_path[0].Item1.center, avoid_path[1].Item1.center, avoid_path[avoid_path.Count - 1].Item1.center);
            // ???????????????????????????????????????????????????????????????
            float angle_second = (float)MathFunction.Angle(avoid_path[0].Item1.center, avoid_path[2].Item1.center, avoid_path[avoid_path.Count - 1].Item1.center);
            // ??????????????????????????????????????????????????????
            float ori_first_dist = (float)MathFunction.Distance(avoid_path[0].Item1.center, avoid_path[1].Item1.center);

            // ???angle_first???angle_second?????????90??????????????????????????????????????????????????????(?????????????????????)?????????????????????????????????????????????
            if (angle_first > 90 && angle_second > 90)
            {
                avoid_path.RemoveAt(1);
            }
            // ???angle_second??????90???????????????angle_first?????????90????????????????????????????????????????????????????????????????????????????????????????????????
            else if (angle_second < 90 && (angle_first > 90 || ori_first_dist < 14.45f))
            {
                // ??????????????????????????????????????????????????????
                float ori_second_dist = (float)MathFunction.Distance(avoid_path[0].Item1.center, avoid_path[2].Item1.center);

                if (ori_second_dist > 14.55f)
                {
                    System.Drawing.PointF new_return_center;
                    System.Drawing.PointF intersection1;
                    System.Drawing.PointF intersection2;
                    int intersections = MathFunction.FindLineCircleIntersections(avoid_path[0].Item1.center.X, avoid_path[0].Item1.center.Y, 14.55f,
                                                                                avoid_path[1].Item1.center, avoid_path[2].Item1.center, out intersection1, out intersection2);

                    if (intersections == 2)
                    {
                        System.Numerics.Vector2 ori_center_intersect = new System.Numerics.Vector2(intersection1.X - avoid_path[0].Item1.center.X,
                                                                                                intersection1.Y - avoid_path[0].Item1.center.Y);

                        System.Numerics.Vector2 ori_center_second_center = new System.Numerics.Vector2(avoid_path[2].Item1.center.X - avoid_path[0].Item1.center.X,
                                                                                                        avoid_path[2].Item1.center.Y - avoid_path[0].Item1.center.Y);

                        if (System.Numerics.Vector2.Dot(ori_center_second_center, ori_center_intersect) > 0)
                        {
                            new_return_center = intersection1;
                        }
                        else
                        {
                            new_return_center = intersection2;
                        }

                        // ????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????
                        MathFunction.Circle new_avoid_center = new MathFunction.Circle(new_return_center, stand_HalfLength / 1000.0f);

                        // ?????????????????????????????????????????????????????????
                        float ori_center_second_center_dist = (float)MathFunction.Distance(avoid_path[0].Item1.center, avoid_path[2].Item1.center);
                        // ?????????????????????????????????????????????????????????
                        float ori_center_new_center_dist = (float)MathFunction.Distance(avoid_path[0].Item1.center, new_avoid_center.center);
                        // ????????????????????????????????????????????????????????????
                        float new_center_second_center_dist = (float)MathFunction.Distance(avoid_path[2].Item1.center, new_avoid_center.center);

                        //????????????????????????(????????????????????????????????????????????????)???????????????????????????????????????(????????????????????????)???
                        //????????????????????????????????????????????????????????????14.45???????????????????????????????????????
                        if (avoid_path[1].Item2 != avoid_path[2].Item2 && new_center_second_center_dist < 14.45f)
                        {
                            avoid_path.RemoveAt(1);
                            return avoid_path;
                        }

                        if (ori_center_second_center_dist > ori_center_new_center_dist)
                        {
                            avoid_path.Insert(1, new System.Tuple<MathFunction.Circle, char>(new_avoid_center, avoid_path[1].Item2));
                            avoid_path.RemoveAt(2);
                            return avoid_path;
                        }
                        else
                        {
                            return avoid_path;

                        }
                    }
                    else
                    {
                        return avoid_path;
                    }
                }
                else if (ori_second_dist < 14.55f && avoid_path[0].Item2 != avoid_path[2].Item2)
                {
                    System.Drawing.PointF new_return_center;
                    System.Drawing.PointF intersection1;
                    System.Drawing.PointF intersection2;
                    int intersections = MathFunction.FindLineCircleIntersections(avoid_path[0].Item1.center.X, avoid_path[0].Item1.center.Y, 14.55f,
                                                                                avoid_path[2].Item1.center, avoid_path[3].Item1.center, out intersection1, out intersection2);

                    if (intersections == 2)
                    {
                        System.Numerics.Vector2 ori_center_intersect = new System.Numerics.Vector2(intersection1.X - avoid_path[0].Item1.center.X,
                                                                                                intersection1.Y - avoid_path[0].Item1.center.Y);

                        System.Numerics.Vector2 ori_center_third_center = new System.Numerics.Vector2(avoid_path[3].Item1.center.X - avoid_path[0].Item1.center.X,
                                                                                                        avoid_path[3].Item1.center.Y - avoid_path[0].Item1.center.Y);

                        if (System.Numerics.Vector2.Dot(ori_center_third_center, ori_center_intersect) > 0)
                        {
                            new_return_center = intersection1;
                        }
                        else
                        {
                            new_return_center = intersection2;
                        }

                        // ????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????
                        MathFunction.Circle new_avoid_center = new MathFunction.Circle(new_return_center, stand_HalfLength / 1000.0f);

                        avoid_path.Insert(3, new System.Tuple<MathFunction.Circle, char>(new_avoid_center, avoid_path[2].Item2));
                        avoid_path.RemoveAt(2);
                        avoid_path.RemoveAt(1);
                        return avoid_path;
                    }
                }
            }
            else
            {
                return avoid_path;
            }
        }
        return avoid_path;
    }

    public (System.Numerics.Vector2, List<System.Tuple<System.Numerics.Vector2, float>>, List<ShipPermutation>) Predict_CV()
    {
        find_Ships_count = findShips.Count;
        predicted_CV = true;
        System.Numerics.Vector2 CV_pos = new System.Numerics.Vector2();
        var Frigate_pos = new List<System.Tuple<System.Numerics.Vector2, float>>();
        List<ShipPermutation> sp_candidates, sp_predictions = new List<ShipPermutation>();

        if (findShips.Count >= 3)
        {
            List<Ship> ships = new List<Ship>();
            for (int i = 0; i < findShips.Count; i++)
            {
                System.Numerics.Vector3 ship_position = new System.Numerics.Vector3(x: findShips[i].pos.x + findShips[i].lostTime * findShips[i].moveVec.x,
                                                                                    y: 0.0f,
                                                                                    z: findShips[i].pos.y + findShips[i].lostTime * findShips[i].moveVec.y) / 1000.0f;
                if (findShips[i].guessName == "CVLL")
                {
                    ships.Add(new Ship(x: ship_position.X, y: ship_position.Z, position_type: "xy", name: "CVLL"));
                }
                else
                {
                    ships.Add(new Ship(x: ship_position.X, y: ship_position.Z, position_type: "xy"));
                }

            }

            // double course = -(180 * Mathf.Atan2(Ship_move_vec.y, Ship_move_vec.x) / Mathf.PI - 90);
            // course = -course - 90;
            // List<Ship> ships = new List<Ship>{
            //     new Ship(x:DetectedShips[0].X, y:DetectedShips[0].Z, position_type:"xy"),
            //     new Ship(x:DetectedShips[1].X, y:DetectedShips[1].Z, position_type:"xy"),
            //     new Ship(x:DetectedShips[2].X, y:DetectedShips[2].Z, position_type:"xy"),
            // };


            (sp_candidates, sp_predictions) = this.predictor.predict(new ShipPermutation(mode: "inference", ships: ships));
            CV_pos = new System.Numerics.Vector2(x: (float)sp_predictions[0].ship_position_predict["CVLL"].x, y: (float)sp_predictions[0].ship_position_predict["CVLL"].y);

            string[] frigates_type = new string[] { "C1", "C2", "D", "A1", "A2" };
            for (int i = 0; i < frigates_type.Length; i++)
            {
                float threaten_radius;
                if (frigates_type[i] == "C1" || frigates_type[i] == "C2" || frigates_type[i] == "D") threaten_radius = 28.0f;
                else threaten_radius = 15.0f;

                Frigate_pos.Add(new System.Tuple<System.Numerics.Vector2, float>(new System.Numerics.Vector2(x: (float)sp_predictions[0].ship_position_predict[frigates_type[i]].x,
                                                                                                            y: (float)sp_predictions[0].ship_position_predict[frigates_type[i]].y),
                                                                                                            threaten_radius));
            }
        }
        else
        {
            for (int i = 0; i < findShips.Count; i++)
            {
                if (findShips[i].guessName == "CVLL")
                {
                    CV_pos = new System.Numerics.Vector2(findShips[i].pos.x + findShips[i].lostTime * findShips[i].moveVec.x,
                                                        findShips[i].pos.y + findShips[i].lostTime * findShips[i].moveVec.y) / 1000.0f;
                }
                else
                {
                    Frigate_pos.Add(new System.Tuple<System.Numerics.Vector2, float>(new System.Numerics.Vector2(x: findShips[i].pos.x + findShips[i].lostTime * findShips[i].moveVec.x,
                                                                                                                y: findShips[i].pos.y + findShips[i].lostTime * findShips[i].moveVec.y) / 1000.0f,
                                                                                                                28.0f));
                }

            }
        }
        return (CV_pos, Frigate_pos, sp_predictions);

    }


    public void SettingHitPath_direct(System.Numerics.Vector2 CV_pos)
    {

        // System.Numerics.Vector2 CV_pos = new System.Numerics.Vector2(x: (float)ships_pos["CVLL"].x, y: (float)ships_pos["CVLL"].y);

        // ??????????????????
        System.Numerics.Vector2 startPos = new System.Numerics.Vector2(x: this.transform.position.x, y: this.transform.position.z) / 1000.0f;
        System.Drawing.PointF startPos_point = new System.Drawing.PointF(x: startPos.X, y: startPos.Y);

        // ??????????????????
        System.Numerics.Vector2 heading_vec = System.Numerics.Vector2.Normalize(new System.Numerics.Vector2(x: this.transform.forward.x, y: this.transform.forward.z));
        float startHeading = -(180 * Mathf.Atan2(this.transform.forward.z, this.transform.forward.x) / Mathf.PI - 90) * (Mathf.PI / 180);

        System.Drawing.PointF second_point = new System.Drawing.PointF(x: startPos_point.X + this.transform.forward.x, y: startPos_point.Y + this.transform.forward.z);

        System.Drawing.PointF CV_point = new System.Drawing.PointF(x: CV_pos.X, y: CV_pos.Y);

        int CV_side = MathFunction.SideOfVector(startPos_point, second_point, CV_point);

        System.Numerics.Vector2 normal_vec;
        // right
        if (CV_side == -1)
        {
            normal_vec = new System.Numerics.Vector2(x: heading_vec.Y, y: -heading_vec.X);
        }
        // left
        else
        {
            normal_vec = new System.Numerics.Vector2(x: -heading_vec.Y, y: heading_vec.X);
        }

        System.Drawing.PointF return_center = new System.Drawing.PointF(x: startPos_point.X + 7.225f * normal_vec.X, y: startPos_point.Y + 7.225f * normal_vec.Y);
        MathFunction.Circle return_circle = new MathFunction.Circle(center: return_center, radius: 7.225f);

        MathFunction.Circle target_circle = new MathFunction.Circle(center: CV_point, radius: 0.1f);

        MathFunction.Line out_tangent_line_1, out_tangent_line_2;
        (out_tangent_line_1, out_tangent_line_2) = MathFunction.CalculateForDifferentRadius(return_circle, target_circle);

        int choose_line = MathFunction.SideOfVector(startPos_point, out_tangent_line_1.PointA, out_tangent_line_1.PointB);

        Vector3 waypoint_1, waypoint_2;
        if (choose_line == CV_side)
        {
            waypoint_1 = new Vector3(x: out_tangent_line_1.PointA.X, y: 0.0f, z: out_tangent_line_1.PointA.Y);
            waypoint_2 = new Vector3(x: out_tangent_line_1.PointB.X, y: 0.0f, z: out_tangent_line_1.PointB.Y);
        }
        else
        {
            waypoint_1 = new Vector3(x: out_tangent_line_2.PointA.X, y: 0.0f, z: out_tangent_line_2.PointA.Y);
            waypoint_2 = new Vector3(x: out_tangent_line_2.PointB.X, y: 0.0f, z: out_tangent_line_2.PointB.Y);
        }
        System.Numerics.Vector2 final_dir_vec = System.Numerics.Vector2.Normalize(new System.Numerics.Vector2(x: waypoint_2.x - waypoint_1.x, y: waypoint_2.z - waypoint_1.z));
        System.Numerics.Vector2 final_dir_normal_vec;
        if (CV_side == -1)
        {
            final_dir_normal_vec = new System.Numerics.Vector2(x: final_dir_vec.Y, y: -final_dir_vec.X);
        }
        else
        {
            final_dir_normal_vec = new System.Numerics.Vector2(x: -final_dir_vec.Y, y: final_dir_vec.X);
        }
        System.Drawing.PointF final_return_center = new System.Drawing.PointF(x: CV_pos.X + 7.225f * final_dir_normal_vec.X, y: CV_pos.Y + 7.225f * final_dir_normal_vec.Y);

        List<PathGroup> pathGroups = GameObject.FindObjectOfType<PathGroupMaker>().pathGroups;

        if (pathGroups.Count == 2)
        {
            for (int i = 0; i < pathGroups[1].Circles.Count; i++)
            {
                pathGroups[1].Circles[i].end = true;
                pathGroups[1].Circles[i].gameObject.active = false;
            }
            pathGroups.RemoveAt(1);
        }

        for (int i = 0; i < pathGroups[0].Circles.Count; i++)
        {
            pathGroups[0].Circles[i].end = true;
            pathGroups[0].Circles[i].gameObject.active = false;
        }

        var path = GameObject.FindObjectOfType<PathGroupMaker>();
        // ????????????????????????????????????????????????????????????
        var avoidPath = new PathSetting();
        avoidPath.name = "avoidPath1";

        var C1 = new CircleData();
        C1.position = new Vector2(x: return_center.X, y: return_center.Y) * 1000;

        var C2 = new CircleData();
        C2.position = new Vector2(x: final_return_center.X, y: final_return_center.Y) * 1000;

        if (CV_side == -1)
        {
            C1.turnMode = TurnMode.Right;
            C2.turnMode = TurnMode.Right;
        }
        else
        {
            C1.turnMode = TurnMode.Left;
            C2.turnMode = TurnMode.Left;
        }
        avoidPath.circleDatas.Add(C1);
        avoidPath.circleDatas.Add(C2);

        var group = new PathGroup();
        // PathGroup????????????????????????????????????
        group.groupName = avoidPath.name;
        var PathGroupMaker = GameObject.FindObjectOfType<PathGroupMaker>();
        for (int i = 0; i < avoidPath.circleDatas.Count; i++)
        {
            var circle = GameObject.Instantiate(PathGroupMaker.turncircle_prefab, new Vector3(avoidPath.circleDatas[i].position.x, 10, avoidPath.circleDatas[i].position.y), new Quaternion().normalized, PathGroupMaker.transform);
            circle.name = avoidPath.name + "_circle" + (i + 1);
            circle.turnMode = avoidPath.circleDatas[i].turnMode;
            circle.pathGroupMaker = PathGroupMaker;
            group.Circles.Add(circle);

        }
        pathGroups.Add(group);
        PathGroupMaker.LinkPathCircles(group.groupName);

        // ???????????????????????????PathGroupMaker??????SettingPathGroup
        // GameObject.FindObjectOfType<PathGroupMaker>().SettingPathGroup(avoidPath);

    }

    public void SettingHitPath_APF(System.Numerics.Vector2 CV_pos, List<System.Tuple<System.Numerics.Vector2, float>> Frigate_pos)
    {

        // ??????????????????
        System.Numerics.Vector2 startPos = new System.Numerics.Vector2(x: this.transform.position.x, y: this.transform.position.z) / 1000.0f;
        System.Drawing.PointF startPos_point = new System.Drawing.PointF(x: startPos.X, y: startPos.Y);

        // ??????????????????
        System.Numerics.Vector2 heading_vec = System.Numerics.Vector2.Normalize(new System.Numerics.Vector2(x: this.transform.forward.x, y: this.transform.forward.z));
        System.Drawing.PointF second_point = new System.Drawing.PointF(x: startPos_point.X + this.transform.forward.x, y: startPos_point.Y + this.transform.forward.z);

        List<System.Tuple<MathFunction.Circle, string, List<System.Drawing.PointF>>> APF_path = Improved_APF.IAPF_returnCircle(Frigate_pos, startPos, heading_vec, CV_pos);

        try
        {
            // ????????????(????????????)?????????????????????????????????????????????????????????????????????
            System.Numerics.Vector2 final_direction_vec = new System.Numerics.Vector2(x: CV_pos.X - APF_path[APF_path.Count - 1].Item3[1].X, y: CV_pos.Y - APF_path[APF_path.Count - 1].Item3[1].Y);
            final_direction_vec = System.Numerics.Vector2.Normalize(final_direction_vec);

            System.Numerics.Vector2 final_direction_normal_vec;
            if (APF_path[APF_path.Count - 1].Item2 == "R")
            {
                final_direction_normal_vec = new System.Numerics.Vector2(x: final_direction_vec.Y, y: -final_direction_vec.X);
            }
            else
            {
                final_direction_normal_vec = new System.Numerics.Vector2(x: -final_direction_vec.Y, y: final_direction_vec.X);
            }

            System.Drawing.PointF final_return_center = new System.Drawing.PointF(x: CV_pos.X + 7.225f * final_direction_normal_vec.X, y: CV_pos.Y + 7.225f * final_direction_normal_vec.Y);

            List<PathGroup> pathGroups = GameObject.FindObjectOfType<PathGroupMaker>().pathGroups;

            if (pathGroups.Count == 2)
            {
                for (int i = 0; i < pathGroups[1].Circles.Count; i++)
                {
                    pathGroups[1].Circles[i].end = true;
                    pathGroups[1].Circles[i].gameObject.active = false;
                }
                pathGroups.RemoveAt(1);
            }

            for (int i = 0; i < pathGroups[0].Circles.Count; i++)
            {
                pathGroups[0].Circles[i].end = true;
                pathGroups[0].Circles[i].gameObject.active = false;
            }

            var path = GameObject.FindObjectOfType<PathGroupMaker>();
            // ????????????????????????????????????????????????????????????
            var avoidPath = new PathSetting();
            avoidPath.name = "avoidPath1";

            var C_first = new CircleData();
            C_first.position = new Vector2(x: APF_path[0].Item1.center.X, y: APF_path[0].Item1.center.Y) * 1000.0f;
            if (APF_path[0].Item2 == "R") C_first.turnMode = TurnMode.Right;
            else C_first.turnMode = TurnMode.Left;
            avoidPath.circleDatas.Add(C_first);

            var C_final = new CircleData();
            C_final.position = new Vector2(x: final_return_center.X, y: final_return_center.Y) * 1000.0f;
            if (APF_path[APF_path.Count - 1].Item2 == "R") C_final.turnMode = TurnMode.Right;
            else C_final.turnMode = TurnMode.Left;
            avoidPath.circleDatas.Add(C_final);

            for (int i = 1; i < APF_path.Count; i++)
            {
                var C = new CircleData();
                C.position = new Vector2(x: APF_path[i].Item1.center.X, y: APF_path[i].Item1.center.Y) * 1000.0f;
                if (APF_path[i].Item2 == "L") C.turnMode = TurnMode.Left;
                else C.turnMode = TurnMode.Right;
                avoidPath.circleDatas.Insert(avoidPath.circleDatas.Count - 1, C);

            }

            var group = new PathGroup();
            // PathGroup????????????????????????????????????
            group.groupName = avoidPath.name;
            var PathGroupMaker = GameObject.FindObjectOfType<PathGroupMaker>();
            for (int i = 0; i < avoidPath.circleDatas.Count; i++)
            {
                var circle = GameObject.Instantiate(PathGroupMaker.turncircle_prefab, new Vector3(avoidPath.circleDatas[i].position.x, 10, avoidPath.circleDatas[i].position.y), new Quaternion().normalized, PathGroupMaker.transform);
                circle.name = avoidPath.name + "_circle" + (i + 1);
                circle.turnMode = avoidPath.circleDatas[i].turnMode;
                circle.pathGroupMaker = PathGroupMaker;
                group.Circles.Add(circle);

            }
            pathGroups.Add(group);
            PathGroupMaker.LinkPathCircles(group.groupName);
        }
        catch (System.ArgumentOutOfRangeException ex)
        {
            Debug.Log("APF??????????????????");
            // throw;
        }
    }

    public (System.Numerics.Vector2, List<System.Tuple<System.Numerics.Vector2, float>>) Reorganize_ships()
    {
        find_Ships_count = findShips.Count;
        System.Numerics.Vector2 CV_pos = new System.Numerics.Vector2();
        List<System.Tuple<System.Numerics.Vector2, float>> Frigate_pos = new List<System.Tuple<System.Numerics.Vector2, float>>();

        for (int i = 0; i < findShips.Count; i++)
        {
            if (findShips[i].guessName == "CVLL")
            {
                CV_pos = new System.Numerics.Vector2(findShips[i].pos.x + findShips[i].lostTime * findShips[i].moveVec.x,
                                                    findShips[i].pos.y + findShips[i].lostTime * findShips[i].moveVec.y) / 1000.0f;
            }
            else
            {
                Frigate_pos.Add(new System.Tuple<System.Numerics.Vector2, float>(new System.Numerics.Vector2(x: findShips[i].pos.x + findShips[i].lostTime * findShips[i].moveVec.x,
                                                                                                            y: findShips[i].pos.y + findShips[i].lostTime * findShips[i].moveVec.y) / 1000.0f,
                                                                                                            28.0f));
            }

        }
        return (CV_pos, Frigate_pos);
    }
    public void Hit_CV(System.Numerics.Vector2 CV_pos, List<System.Tuple<System.Numerics.Vector2, float>> Frigate_pos)
    {
        System.Drawing.PointF CV_point = new System.Drawing.PointF(CV_pos.X, CV_pos.Y);
        System.Drawing.PointF now_pos = new System.Drawing.PointF(this.transform.position.x / 1000.0f, this.transform.position.z / 1000.0f);

        float dist2CV = (float)MathFunction.Distance(now_pos, CV_point);

        if (dist2CV < 15.0f || Frigate_pos.Count == 0)
        {

            SettingHitPath_direct(CV_pos);
        }
        else
        {
            SettingHitPath_APF(CV_pos, Frigate_pos);
        }
    }
    public void ModifyPath(Vector3 ship_pos)
    {
        List<PathGroup> pathGroups = GameObject.FindObjectOfType<PathGroupMaker>().pathGroups;
        var PathGroupMaker = GameObject.FindObjectOfType<PathGroupMaker>();
        int connect_back_idx = 6;

        if (pathGroups[0].Circles[1].pointIn.isActiveAndEnabled == true && early_stop == false)
        {
            for (int i = pathGroups[0].Circles.Count - 1; i > 1; i--)
            {
                GameObject.Destroy(pathGroups[0].Circles[i - 1].pointOut.gameObject);
                pathGroups[0].Circles[i].end = true;
                pathGroups[0].Circles[i].gameObject.active = false;
                pathGroups[0].Circles.RemoveAt(i);
            }

            List<Vector3> early_stop_circle_list = new List<Vector3>();
            early_stop_circle_list.Add(new Vector3(-52775.0f, 10.0f, 0.0f));
            early_stop_circle_list.Add(new Vector3(-26387.5f, 10.0f, -26387.5f));
            early_stop_circle_list.Add(new Vector3(0.0f, 10.0f, 0.0f));

            string group_name = pathGroups[0].groupName;
            for (int i = 0; i < early_stop_circle_list.Count; i++)
            {
                var circle = GameObject.Instantiate(PathGroupMaker.turncircle_prefab, new Vector3(early_stop_circle_list[i].x, 10, early_stop_circle_list[i].z), new Quaternion().normalized, PathGroupMaker.transform);
                circle.name = group_name + "_circle" + (pathGroups[0].Circles.Count + 1);
                circle.turnMode = pathGroups[0].Circles[pathGroups[0].Circles.Count - 1].turnMode;
                circle.pathGroupMaker = PathGroupMaker;
                pathGroups[0].Circles.Add(circle);
                pathGroups[0].Circles[pathGroups[0].Circles.Count - 2].LinkNext(pathGroups[0].Circles[pathGroups[0].Circles.Count - 1]);

            }
            early_stop = true;
            early_stop_level = 1;
        }
        else if (pathGroups[0].Circles[2].pointIn.isActiveAndEnabled == true && early_stop == false)
        {
            for (int i = pathGroups[0].Circles.Count - 1; i > 2; i--)
            {
                GameObject.Destroy(pathGroups[0].Circles[i - 1].pointOut.gameObject);
                pathGroups[0].Circles[i].end = true;
                pathGroups[0].Circles[i].gameObject.active = false;
                pathGroups[0].Circles.RemoveAt(i);
            }

            List<Vector3> early_stop_circle_list = new List<Vector3>();
            early_stop_circle_list.Add(new Vector3(-52775.0f, 10.0f, 0.0f));
            early_stop_circle_list.Add(new Vector3(0.0f, 10.0f, 0.0f));

            string group_name = pathGroups[0].groupName;
            for (int i = 0; i < early_stop_circle_list.Count; i++)
            {
                var circle = GameObject.Instantiate(PathGroupMaker.turncircle_prefab, new Vector3(early_stop_circle_list[i].x, 10, early_stop_circle_list[i].z), new Quaternion().normalized, PathGroupMaker.transform);
                circle.name = group_name + "_circle" + (pathGroups[0].Circles.Count + 1);
                circle.turnMode = pathGroups[0].Circles[pathGroups[0].Circles.Count - 1].turnMode;
                circle.pathGroupMaker = PathGroupMaker;
                pathGroups[0].Circles.Add(circle);
                pathGroups[0].Circles[pathGroups[0].Circles.Count - 2].LinkNext(pathGroups[0].Circles[pathGroups[0].Circles.Count - 1]);

            }
            early_stop = true;
            early_stop_level = 2;

        }

        connect_back_idx = (early_stop == true) ? 5 : 6;
        if (pathGroups[0].Circles.Count == 7)
        {
            // if (pathGroups[0].Circles[connect_back_idx - 1].end == false || (pathGroups[0].Circles[connect_back_idx - 1].end == true && pathGroups.Count == 2))
            if (pathGroups[0].Circles[connect_back_idx - 1].end == false)
            {
                // GameObject.Destroy(pathGroups[0].Circles[connect_back_idx - 1].pointOut.gameObject);
                // pathGroups[0].Circles[connect_back_idx].end = true;
                // pathGroups[0].Circles[connect_back_idx].gameObject.active = false;
                // pathGroups[0].Circles.RemoveAt(connect_back_idx);

                int remove_time = 1;
                if (early_stop == true)
                    remove_time = 2;

                for (int i = 0; i < remove_time; i++)
                {
                    GameObject.Destroy(pathGroups[0].Circles[pathGroups[0].Circles.Count - 2].pointOut.gameObject);
                    pathGroups[0].Circles[pathGroups[0].Circles.Count - 1].end = true;
                    pathGroups[0].Circles[pathGroups[0].Circles.Count - 1].gameObject.active = false;
                    pathGroups[0].Circles.RemoveAt(pathGroups[0].Circles.Count - 1);
                }

                Vector2 middle_point = new Vector2(0.0f, 0.0f);
                for (int i = 0; i < findShips.Count; i++)
                {
                    Vector2 ship_pos_update = findShips[i].pos + (findShips[i].lostTime * findShips[i].moveVec);
                    middle_point += ship_pos_update;
                }
                middle_point /= findShips.Count;

                string group_name = pathGroups[0].groupName;
                var circle = GameObject.Instantiate(PathGroupMaker.turncircle_prefab, new Vector3(middle_point.x, 10, middle_point.y), new Quaternion().normalized, PathGroupMaker.transform);
                circle.name = group_name + "_circle" + (pathGroups[0].Circles.Count + 1);
                circle.pathGroupMaker = PathGroupMaker;

                if (pathGroups[0].Circles[pathGroups[0].Circles.Count - 1].end == false)
                {
                    circle.turnMode = pathGroups[0].Circles[pathGroups[0].Circles.Count - 2].turnMode;
                    pathGroups[0].Circles.Add(circle);
                    pathGroups[0].Circles[pathGroups[0].Circles.Count - 2].LinkNext(pathGroups[0].Circles[pathGroups[0].Circles.Count - 1]);
                }
                // else if (pathGroups[0].Circles[pathGroups[0].Circles.Count - 1].end == true && pathGroups.Count == 2)
                // {
                //     circle.turnMode = pathGroups[1].Circles[pathGroups[1].Circles.Count - 1].turnMode;
                //     pathGroups[0].Circles.Add(circle);
                //     pathGroups[1].Circles[pathGroups[1].Circles.Count - 1].LinkNext(pathGroups[0].Circles[pathGroups[0].Circles.Count - 1]);
                // }
            }

        }
        else
        {
            string group_name = pathGroups[0].groupName;
            var circle = GameObject.Instantiate(PathGroupMaker.turncircle_prefab, new Vector3(ship_pos.x, 10, ship_pos.z), new Quaternion().normalized, PathGroupMaker.transform);
            circle.name = group_name + "_circle" + (pathGroups[0].Circles.Count + 1);
            circle.turnMode = TurnMode.Left;
            circle.pathGroupMaker = PathGroupMaker;
            pathGroups[0].Circles.Add(circle);
            pathGroups[0].Circles[pathGroups[0].Circles.Count - 2].LinkNext(pathGroups[0].Circles[pathGroups[0].Circles.Count - 1]);
        }

        if (early_stop == true && pathGroups[0].Circles.Count == 6)
        {
            Vector2 shipped_circle;
            if (early_stop_level == 1)
            {
                shipped_circle = new Vector2(0.0f, 52775.0f);
            }
            else
            {
                shipped_circle = new Vector2(-26387.5f, -26387.5f);
            }
            string group_name = pathGroups[0].groupName;
            var circle = GameObject.Instantiate(PathGroupMaker.turncircle_prefab, new Vector3(shipped_circle.x, 10, shipped_circle.y), new Quaternion().normalized, PathGroupMaker.transform);
            circle.name = group_name + "_circle" + (pathGroups[0].Circles.Count + 1);
            circle.turnMode = TurnMode.Left;
            circle.pathGroupMaker = PathGroupMaker;
            pathGroups[0].Circles.Add(circle);
            pathGroups[0].Circles[pathGroups[0].Circles.Count - 2].LinkNext(pathGroups[0].Circles[pathGroups[0].Circles.Count - 1]);
        }
    }

    public string ModifyPath_surround_ship(Vector3 ship_pos)
    {
        if (find_Ships_count != findShips.Count)
        {
            find_Ships_count = findShips.Count;

            // ??????????????????
            System.Numerics.Vector3 startPos = new System.Numerics.Vector3(x: this.transform.position.x, y: this.transform.position.y, z: this.transform.position.z) / 1000.0f;
            System.Drawing.PointF startPos_point = new System.Drawing.PointF(x: startPos.X, y: startPos.Z);

            // ??????????????????
            System.Numerics.Vector2 heading_vec = System.Numerics.Vector2.Normalize(new System.Numerics.Vector2(x: this.transform.forward.x, y: this.transform.forward.z));

            System.Drawing.PointF second_point = new System.Drawing.PointF(x: startPos_point.X + this.transform.forward.x, y: startPos_point.Y + this.transform.forward.z);

            System.Drawing.PointF Corvette_point = new System.Drawing.PointF(x: findShips[findShips.Count - 1].pos.x / 1000.0f, y: findShips[findShips.Count - 1].pos.y / 1000.0f);

            System.Drawing.PointF Corvettes_center_gravity = new System.Drawing.PointF(0, 0);
            for (int i = 0; i < findShips.Count; i++)
            {
                Corvettes_center_gravity = new System.Drawing.PointF(Corvettes_center_gravity.X + findShips[i].pos.x / 1000.0f, Corvettes_center_gravity.Y + findShips[i].pos.y / 1000.0f);
            }
            Corvettes_center_gravity = new System.Drawing.PointF(Corvettes_center_gravity.X / findShips.Count, Corvettes_center_gravity.Y / findShips.Count);

            int Corvette_side = MathFunction.SideOfVector(startPos_point, second_point, Corvettes_center_gravity);

            System.Numerics.Vector2 missile2Corvette = System.Numerics.Vector2.Normalize(new System.Numerics.Vector2(x: startPos_point.X - Corvette_point.X, y: startPos_point.Y - Corvette_point.Y));
            List<System.Numerics.Vector2> normal_vec = new List<System.Numerics.Vector2>();
            int circle_numbers = 3;
            float single_angle = Mathf.PI * (360.0f / circle_numbers) / 180.0f;
            char turn_side;
            // Corvette is on the right of the missile, then turn left first
            if (Corvette_side == -1)
            {
                for (int i = circle_numbers - 1; i >= 0; i--)
                {
                    float x = missile2Corvette.X * Mathf.Cos(single_angle * i) - missile2Corvette.Y * Mathf.Sin(single_angle * i);
                    float y = missile2Corvette.X * Mathf.Sin(single_angle * i) + missile2Corvette.Y * Mathf.Cos(single_angle * i);
                    System.Numerics.Vector2 next_vec = new System.Numerics.Vector2(x, y);
                    normal_vec.Add(next_vec);
                }
                turn_side = 'R';
            }
            // Corvette is on the left of the missile, then turn right first
            else
            {
                for (int i = 0; i < circle_numbers; i++)
                {
                    float x = missile2Corvette.X * Mathf.Cos(single_angle * (i + 1)) - missile2Corvette.Y * Mathf.Sin(single_angle * (i + 1));
                    float y = missile2Corvette.X * Mathf.Sin(single_angle * (i + 1)) + missile2Corvette.Y * Mathf.Cos(single_angle * (i + 1));
                    System.Numerics.Vector2 next_vec = new System.Numerics.Vector2(x, y);
                    normal_vec.Add(next_vec);
                }
                turn_side = 'L';
            }

            List<PathGroup> pathGroups = GameObject.FindObjectOfType<PathGroupMaker>().pathGroups;
            var PathGroupMaker = GameObject.FindObjectOfType<PathGroupMaker>();

            if (pathGroups.Count == 2)
            {
                for (int i = 0; i < pathGroups[1].Circles.Count; i++)
                {
                    pathGroups[1].Circles[i].end = true;
                    pathGroups[1].Circles[i].gameObject.active = false;
                }
                pathGroups.RemoveAt(1);
            }

            for (int i = 0; i < pathGroups[0].Circles.Count; i++)
            {
                pathGroups[0].Circles[i].end = true;
                pathGroups[0].Circles[i].gameObject.active = false;
            }


            var group = new PathGroup();
            // PathGroup????????????????????????????????????
            group.groupName = pathGroups[0].groupName;
            pathGroups.RemoveAt(0);
            for (int i = 0; i < normal_vec.Count + 1; i++)
            {
                System.Numerics.Vector2 return_center;
                if (i != normal_vec.Count)
                {
                    return_center = new System.Numerics.Vector2(x: Corvette_point.X + 35.325f * normal_vec[i].X, y: Corvette_point.Y + 35.325f * normal_vec[i].Y) * 1000.0f;

                    if (normal_vec.Count > 1)
                    {
                        System.Numerics.Vector2 old_Corvette = new System.Numerics.Vector2(findShips[0].pos.x, findShips[0].pos.y);
                        float old_Corvette_dist = System.Numerics.Vector2.Distance(return_center, old_Corvette);
                        if (old_Corvette_dist < 35225.0f)
                        {
                            System.Drawing.PointF lineStart = new System.Drawing.PointF(x: Corvette_point.X * 1000.0f, y: Corvette_point.Y * 1000.0f);
                            System.Drawing.PointF lineEnd = new System.Drawing.PointF(x: return_center.X, y: return_center.Y);
                            System.Drawing.PointF intersection1;
                            System.Drawing.PointF intersection2;
                            int intersections = MathFunction.FindLineCircleIntersections(old_Corvette.X, old_Corvette.Y, 35325.0f, lineStart, lineEnd, out intersection1, out intersection2);
                            float new_Corvette_dist = (float)MathFunction.Distance(lineStart, intersection1);

                            return_center = (new_Corvette_dist > 35225.0f) ? new System.Numerics.Vector2(intersection1.X, intersection1.Y) : new System.Numerics.Vector2(intersection2.X, intersection2.Y);
                        }
                    }
                }
                else
                {
                    return_center = new System.Numerics.Vector2(x: Corvette_point.X, y: Corvette_point.Y) * 1000.0f;
                }

                var circle = GameObject.Instantiate(PathGroupMaker.turncircle_prefab, new Vector3(return_center.X, 10, return_center.Y), new Quaternion().normalized, PathGroupMaker.transform);
                circle.name = group.groupName + "_circle" + (i + 1);
                if (turn_side == 'R')
                    circle.turnMode = TurnMode.Right;
                else
                    circle.turnMode = TurnMode.Left;
                circle.pathGroupMaker = PathGroupMaker;
                group.Circles.Add(circle);
            }
            pathGroups.Add(group);
            PathGroupMaker.LinkPathCircles(group.groupName);

            if (turn_side == 'R')
                return "LSR";
            else
                return "RSL";
        }
        return "";
    }

    public bool CheckAvoidNeed(Vector2 ship_pos, float range)
    {
        //?????????????????? ax+by+c = 0
        Vector2 self_2D_pos = new Vector2(this.transform.position.x, this.transform.position.z);
        Vector2 self_f = new Vector2(this.transform.forward.x, this.transform.forward.z);

        var a = self_f.y;
        var b = -1 * self_f.x;
        var c = -1 * a * self_2D_pos.x - b * self_2D_pos.y;

        var dis = Mathf.Abs(a * ship_pos.x + b * ship_pos.y + c) / Mathf.Sqrt((a * a + b * b));

        if (dis >= range)
            return false;
        else
            return true;
    }

    public void RF_Finded(ShipWork ship)
    {
        Vector2 find_pos = new Vector2(ship.transform.position.x, ship.transform.position.z);

        var newData = true;
        for (int i = 0; i < findShips.Count; i++)
        {
            /*
            if (findShips[i].first)
            {
                var dis = Vector2.Distance(find_pos, findShips[i].pos);
                if (dis < 100)
                {
                    findShips[i].first = false;
                    findShips[i].moveVec = (find_pos - findShips[i].pos) / 4;
                    findShips[i].pos = find_pos;
                    newData = false;
                    findShips[i].lostTime = 0.0f;
                    i = findShips.Count;
                }
            }
            else
            {*/
            var dis = Vector2.Distance(find_pos, findShips[i].pos + (findShips[i].lostTime * findShips[i].moveVec));
            var vecLen = Vector2.Distance(new Vector2(0, 0), findShips[i].moveVec);

            // Debug.Log("dis:" + dis + " len:" + vecLen);

            // if (dis <= (vecLen * 10))
            if (dis <= 5000.0f)
            {
                findShips[i].pos = find_pos;
                findShips[i].lostTime = 0.0f;
                newData = false;
                i = findShips.Count;
            }
            // }
        }

        if (newData)
        {
            if (ship.shipName != "CVLL")
            {
                var newShip = new FindShip();
                newShip.guessName = "other";
                newShip.pos = find_pos;
                newShip.moveVec = new Vector2(ship.transform.forward.x, ship.transform.forward.z) * ship.shipSpeed * 0.5144f;
                newShip.lostTime = 0.0f;
                findShips.Add(newShip);
            }
            else
            {
                var newShip = new FindShip();
                newShip.guessName = ship.shipName;
                newShip.pos = find_pos;
                newShip.moveVec = new Vector2(ship.transform.forward.x, ship.transform.forward.z) * ship.shipSpeed * 0.5144f;
                newShip.lostTime = 0.0f;
                findShips.Add(newShip);
            }
        }

    }

    private void OnGUI()
    {
        if (text_x != null)
        {
            if (Mathf.Abs(x_value) >= 1000)
            {
                text_x.text = (this.transform.transform.position.x / 1000).ToString("0.0");
                text_x_stand.text = "km";
            }
            else
            {
                text_x.text = (this.transform.transform.position.x).ToString("0.0");
                text_x_stand.text = "m";
            }
        }
        if (text_z != null)
        {
            if (Mathf.Abs(z_value) >= 1000)
            {
                text_z.text = (this.transform.transform.position.z / 1000).ToString("0.0");
                text_z_stand.text = "km";
            }
            else
            {
                text_z.text = (this.transform.transform.position.z).ToString("0.0");
                text_z_stand.text = "m";
            }
        }
        if (text_h != null)
        {
            if (Mathf.Abs(h_value) >= 1000)
            {
                text_h.text = (this.transform.transform.position.y / 1000).ToString("0.0");
                text_h_stand.text = "km";
            }
            else
            {
                text_h.text = (this.transform.transform.position.y).ToString("0.0");
                text_h_stand.text = "m";
            }
        }
        if (text_v != null)
            text_v.text = speed.ToString("0.0");
        if (text_r != null)
            text_r.text = this.transform.eulerAngles.y.ToString("0.0");
        if (imagePoint != null)
            imagePoint.transform.localEulerAngles = new Vector3(0.0f, 0.0f, -1 * this.transform.localEulerAngles.y);

        if (text_length != null)
            text_length.text = "???????????????: " + (moveLength / 1000.0f).ToString("0.0") + "Km";
    }

    // Update is called once per frame
    void Update()
    {
        if (limitFPS)
        {
            if (Application.targetFrameRate != 60)
                Application.targetFrameRate = 60;
        }
        animator.SetFloat("Up", up);
        animator.SetFloat("Right", right);

        if ((right == 0.0f) && (execute_avoid_tatic) && (!ship_wait_to_solve.Equals(new Vector3(0, 0, 0))) && predicted_CV == false && enmyTarget == null && startSimulator)
            SettingAvoidPath(ship_wait_to_solve);

        if (work)
        {
            work = false;
            StartSimulator();
        }
        x_value = this.transform.transform.position.x;
        z_value = this.transform.transform.position.z;
        h_value = this.transform.transform.position.y;


    }

    public void ChangeR(float value)
    {
        if (!startSimulator)
            return;
        if (value > 45.0f)
            right = 45.0f;
        else if (value < -45.0f)
            right = -45.0f;
        else
            right = value;
    }
}


[System.Serializable]
public class FindShip
{
    public Vector2 pos = new Vector2();
    public string guessName = ""; //054A 052C 052D CVLL
    public Vector2 moveVec = new Vector2();
    public float lostTime;
}