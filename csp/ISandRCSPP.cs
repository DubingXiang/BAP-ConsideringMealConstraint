using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;

namespace CG_CSP_1440
{
    using common_db;
    class InitialSolution
    {
        //输出,传的应该是 out来传
        public List<int[]> A_Matrix;
        public List<double> Coefs;
        public List<Pairing> PathSet;

        public double initial_ObjValue;


        //输入变量
        NetWork Net;
        List<Node> NodeSet;
        //List<Node> TripList;//目标点集。依次以tripList中的点为起点，求其顺逆向寻最短路
        List<Node> LineList;
        List<int> LineIDList;

        public InitialSolution(NetWork Network) {
            Net = Network;
            NodeSet = Net.NodeSet;
            LineList = new List<Node>();
            LineIDList = new List<int>();
            PathSet = new List<Pairing>();
            //TripList = Net.TripList;            
            for (int i = 0; i < Net.TripList.Count && LineIDList.Count != NetWork.num_Physical_trip; i++) {
                //LineList.Add(Net.TripList[i]);                
                if (LineIDList.Contains(Net.TripList[i].LineID) == false) {
                    LineIDList.Add(Net.TripList[i].LineID);
                }
            }
            for (int i = 0; i < Net.TripList.Count / Net.CrewRules.MaxDays; i++) {
                if (LineList.Contains(Net.TripList[i]) == false) {
                    LineList.Add(Net.TripList[i]);
                }
            }

        }

        public List<Pairing> GetFeasibleSolutionByMethod1() {
            //中间变量，用来传值
            Node trip = new Node();
            Pairing loopPath;
            int i, j;
            //Node s = NodeSet[0];
            Topological2 topo;
            RCSPP R_C_SPP = new RCSPP(Net, out topo);
            //R_C_SPP.UnProcessed = new List<Node>();
            //for (i = 0; i < Topo.Order.Count; i++) {
            //    R_C_SPP.UnProcessed.Add(Topo.Order[i]);
            //}
            R_C_SPP.ShortestPath("Forward");

            //R_C_SPP.UnProcessed = Topo.Order; //TODO:测试 2-24-2019

            R_C_SPP.ShortestPath("Backward");

            //for (i = 0; i < NodeSet.Count; i++) 
            //{
            //    trip = NodeSet[i];
            //    trip.Visited = false;
            //    if (trip.ID != 0 && trip.ID != -1) {
            //        TripList.Add(trip);
            //    }
            //}
            //也按拓扑顺序否？？
            while (LineList.Count > 0) {
                trip = LineList[0];//这里以 1,2,3...顺序寻路，使得许多路的大部分内容相同，可不可以改进策略                
                loopPath = FindFeasiblePairings(trip);
                LineList.RemoveAt(0);
                if (loopPath.ArcList == null) {
                    throw new Exception("找不到可行回路！咋办啊！！");
                }
                else {
                    PathSet.Add(loopPath);
                    for (i = 0; i < loopPath.ArcList.Count; i++) {
                        trip = loopPath.ArcList[i].D_Point;
                        for (j = 0; j < LineList.Count; j++) {
                            if (LineList[j].ID == trip.ID) {
                                //trip.Visited = true;
                                LineList.RemoveAt(j);
                                break;
                            }
                        }
                    }
                }
            }

            PrepareInputForRMP(Net.TripList);

            return this.PathSet;
        }
        Pairing FindFeasiblePairings(Node trip) {
            Pairing loopPath = new Pairing
            {
                ArcList = new List<Arc>()
            };//output
            int i, j;
            int minF = 0, minB = 0;
            Label labelF, labelB;
            Arc arc;

            Double AccumuDrive, T3, C;
            double MAX = 666666;
            Double Coef = MAX;
            for (i = 0; i < trip.LabelsForward.Count; i++) {
                labelF = trip.LabelsForward[i];
                for (j = 0; j < trip.LabelsBackward.Count; j++) {
                    labelB = trip.LabelsBackward[j];
                    if (labelF.BaseOfCurrentPath.Station != labelB.BaseOfCurrentPath.Station) {
                        continue;
                    }
                    AccumuDrive = labelF.AccumuDrive + labelB.AccumuDrive - trip.Length;
                    T3 = labelF.AccumuWork + labelB.AccumuWork - trip.Length;
                    C = labelF.AccumuCost + labelB.AccumuCost - trip.Length;
                    //求初始解时，Cost即为非乘务时间，即目标函数，而在列生成迭代中，非也（因为对偶乘子）                  
                    if (AccumuDrive <= Net.CrewRules.PureCrewTime &&
                        T3 <= Net.CrewRules.TotalCrewTime &&
                        Coef >= C) //find minmal cost
                    {
                        minF = i;
                        minB = j;
                        Coef = C;

                    }
                }
            }
            if (Coef < MAX) {
                labelF = trip.LabelsForward[minF];
                labelB = trip.LabelsBackward[minB];
                loopPath.Coef = 1440;
                int pathday = 1;
                arc = labelF.PreEdge;
                while (arc.O_Point.ID != 0) {
                    pathday = arc.ArcType == 22 ? pathday + 1 : pathday;
                    loopPath.ArcList.Insert(0, arc);
                    labelF = labelF.PreLabel;
                    arc = labelF.PreEdge;
                }
                loopPath.ArcList.Insert(0, arc);

                arc = labelB.PreEdge;
                while (arc.D_Point.ID != 1) {
                    pathday = arc.ArcType == 22 ? pathday + 1 : pathday;
                    loopPath.ArcList.Add(arc);
                    labelB = labelB.PreLabel;
                    arc = labelB.PreEdge;
                }
                loopPath.ArcList.Add(arc);

                loopPath.Coef *= pathday;
            }

            if (loopPath.ArcList.Count == 0) {
                loopPath = default(Pairing);
            }

            return loopPath;
        }

        //Pairing FindFeasiblePairing_no_backward()

        public List<Pairing> GetFeasibleSolutionByPenalty() {
            Node trip = new Node();
            Pairing Pairing;
            int i;

            Topological2 topo;
            RCSPP R_C_SPP = new RCSPP(Net, out topo); //TODO:测试 2-24-2019            
            int M = 99999;
            R_C_SPP.ChooseCostDefinition(0);
            Arc arc;

            #region heuristisc when find a path becoming difficult
            int max_nb_nochange = 3;
            int max_nb2_nochange = 10;
            #endregion

            //迭代，直到所有trip被cover
            int last_LineListcount;
            //while (LineList.Count > 0) 
            while (LineIDList.Count > 0) {
                Console.Write(LineIDList.Count + ", ");
                last_LineListcount = LineIDList.Count;

                if (max_nb2_nochange == 10) {
                    foreach (var node in this.NodeSet) {
                        node.numVisited = 0;
                        node.Price = 0;
                    }
                    max_nb2_nochange = 10;
                }

                if (max_nb_nochange == 0) {
                    foreach (var id in LineIDList) {
                        Net.TripList[id - 1].Price += M;
                    }

                    max_nb_nochange = 3;
                }

                ////debug
                //if (last_LineListcount <= 9) {
                //    foreach (var label in NodeSet[1].LabelsForward) {
                //        Label temp = label;
                //        arc = temp.PreEdge;
                //        Console.WriteLine("到终点所有的路径：");
                //        while (arc.O_Point.ID != 0) {
                //            Console.Write(arc.O_Point.LineID + " <-");
                //            temp = temp.PreLabel;
                //            arc = temp.PreEdge;
                //        }
                //        Console.WriteLine("end");
                //    }
                //}
                //// end debug

                R_C_SPP.ShortestPath("Forward");
                R_C_SPP.FindNewPath();

                Pairing = R_C_SPP.New_Column;
                //TODO:若当前找到的路包含的点均已被PathSet里的路所包含，就是说该条路没有囊括新的点，那就不添加到PathSet中

                for (i = 1; i < Pairing.ArcList.Count - 2; i++) //起终点不用算
                {
                    arc = Pairing.ArcList[i];
                    //需还原pairing的Cost，减去 当前 增加的 M 的部分，即price
                    if (arc.D_Point.numVisited > 0) {
                        Pairing.Cost += arc.D_Point.Price;
                    }

                    arc.D_Point.numVisited++;
                    arc.D_Point.Price = -arc.D_Point.numVisited * M;
                    //第二天对应的复制点也要改变
                    if (Net.CrewRules.MaxDays > 1)//&& arc.D_Point.StartTime < 1440) 
                    {
                        for (int j = 0; j < NodeSet.Count; j++) {
                            if (NodeSet[j].LineID == arc.D_Point.LineID && NodeSet[j].StartTime > arc.D_Point.StartTime) {
                                NodeSet[j].numVisited++;
                                NodeSet[j].Price = -NodeSet[j].numVisited * M;
                            }
                        }
                    }

                    //LineList.Remove(arc.D_Point);
                    LineIDList.Remove(arc.D_Point.LineID);
                }
                if (last_LineListcount > LineIDList.Count) {
                    PathSet.Add(Pairing);
                }
                else {
                    --max_nb_nochange;
                    --max_nb2_nochange;
                }

            }
            //还原trip的price
            foreach (var node in this.NodeSet) {
                node.numVisited = 0;
                node.Price = 0;
            }

            PrepareInputForRMP(Net.TripList);

            return this.PathSet;
        }


        public List<Pairing> GetVirtualPathSetAsInitSoln() {
            for (int i = 0; i < NetWork.num_Physical_trip; i++) {
                Pairing vir_path = new Pairing() { ArcList = new List<Arc>() };
                vir_path.Coef = 100000;
                vir_path.CoverMatrix = new int[NetWork.num_Physical_trip];

                PathSet.Add(vir_path);

                vir_path.CoverMatrix[i] = 1;
            }
            PrepareInputForRMP(Net.TripList);

            A_Matrix.Clear();
            foreach (var vir_path in PathSet) {
                A_Matrix.Add(vir_path.CoverMatrix);
            }

            return this.PathSet;
        }


        private void PrepareInputForRMP(List<Node> TripList) //2-21-2019改前是 ref
        {
            //Get Coef in FindAllPaths
            Coefs = new List<double>();
            A_Matrix = new List<int[]>();
            int realistic_trip_num = NetWork.num_Physical_trip;
            foreach (var path in PathSet) {
                //Coefs.Add(path.Cost);
                Coefs.Add(path.Coef);

                int[] a = new int[realistic_trip_num];
                for (int i = 0; i < realistic_trip_num; i++) {
                    a[i] = 0;
                }

                foreach (Node trip in TripList) {
                    foreach (Arc arc in path.ArcList) {
                        if (arc.D_Point == trip) {
                            a[trip.LineID - 1] = 1;
                        }
                    }
                }

                A_Matrix.Add(a);
            }

            GetObjValue();
        }

        private double GetObjValue() {
            foreach (var c in Coefs) {
                initial_ObjValue += c;
            }
            return initial_ObjValue;
        }

    }

    //资源约束最短路
    class RCSPP
    {
        //OUTPUT
        public double Reduced_Cost = 0;
        public Pairing New_Column;
        //public int[] newAji;
        //public int[,] newMultiAji;

        //添加的

        public List<Pairing> New_Columns;
        List<Label> negetiveLabels;
        //public double[] reduced_costs;

        //与外界相关联的
        public List<Node> UnProcessed = new List<Node>();//Topo序列

        public List<CrewBase> CrewbaseList = DataReader.CrewBaseList; //2-20-2019

        CrewRules crew_rules;
        double accumuConsecDrive, accumuDrive, accumuWork, C;//资源向量,cost
        bool resource_feasible;
        string direction;

        //public Dictionary<string, int> costDefinition = new Dictionary<string, int>();
        int costType;

        public RCSPP(NetWork net, out Topological2 topological) {
            crew_rules = net.CrewRules;
            Node s = net.NodeSet[0];
            topological = new Topological2(net, s);

            foreach (Node node in topological.Order) {
                UnProcessed.Add(node);
            }
        }

        /// <summary>
        ///  definetype in [0,2],选择成本的具体定义。//（此前可定义一个字典，选择定义）
        /// </summary>
        /// <param name="defineType"></param>
        public void ChooseCostDefinition(int defineType) {
            try {
                if (0 <= defineType && defineType <= 2) { this.costType = defineType; }
                else { throw new System.Exception("参数输入错误, defineType must in [0, 2]"); }
            }
            catch (Exception ex) {
                Console.WriteLine(ex.Message);
            }
        }

        public void ShortestPath(string Direction) //Forward;Backward
        {
            direction = Direction;
            Node trip1, trip2;
            Label label1, label2;
            int i, j;

            InitializeStartNode();
            //framework of labeling setting algorithm                                                
            if (direction == "Forward") {
                for (int t = 0; t < UnProcessed.Count; t++) {
                    trip1 = UnProcessed[t];
                    //2-20-2019                       
                    if (trip1.Type == 0) //基地起点
                    {
                        InitializeBaseNode(trip1, trip1.LabelsForward);
                    }
                    //end
                    //dominated rule
                    //这里可以再想想优化,实际上类似于排序，采用类似于归并排序的处理,本质是分治思想
                    #region  未优化的，直接两两比较，复杂度 O(n^2)(实际共比较n(n-1)/2次)
                    //for (int l1 = 0; l1 < trip1.LabelsForward.Count; l1++)
                    //{
                    //    for (int l2 = l1 + 1; l2 < trip1.LabelsForward.Count; l2++)
                    //    {
                    //        DominateRule(trip1.LabelsForward[l1], trip1.LabelsForward[l2]);
                    //    }
                    //}
                    //for (i = 0; i < trip1.LabelsForward.Count; i++)
                    //{
                    //    if (trip1.LabelsForward[i].Dominated == true)
                    //    {
                    //        trip1.LabelsForward.RemoveAt(i);
                    //        i--;
                    //    }
                    //}
                    #endregion
                    //优化后，速度提高了10多倍
                    RemainPateroLabels(ref trip1.LabelsForward);

                    //判断是否可延伸，即是否 resource-feasible
                    for (i = 0; i < trip1.Out_Edges.Count; i++) {
                        Arc arc = trip1.Out_Edges[i];
                        trip2 = arc.D_Point;

                        //Console.WriteLine("trip1:" + Logger.TripInfoToStr(trip1));
                        //Console.WriteLine("trip2:" + Logger.TripInfoToStr(trip2));

                        //if (arc.O_Point.TrainCode == "G7326" || arc.O_Point.TrainCode == "G7325") {
                        //    Console.WriteLine("trip1:" + Logger.TripInfoToStr(trip1));
                        //    Console.WriteLine("trip2:" + Logger.TripInfoToStr(trip2));
                        //    int yy = 0;
                        //}

                        for (j = 0; j < trip1.LabelsForward.Count; j++) {
                            label1 = trip1.LabelsForward[j];

                            //if (arc.O_Point.TrainCode == "G9505" && arc.D_Point.TrainCode == "G9506") {
                            //    Console.WriteLine(Logger.LabelInfoToStr(label1));
                            //}

                            resource_feasible = false;
                            label2 = REF(label1, trip2, arc);

                            if (resource_feasible)//label1可延伸至trip2
                            {

                                trip2.LabelsForward.Add(label2);
                            }
                            else { label2 = default(Label); }
                        }
                    }
                }
            }
            else if (direction == "Backward") {
                while (UnProcessed.Count > 0) {
                    trip1 = UnProcessed[0];

                    if (trip1.Type == 2) //基地终点
                    {
                        InitializeBaseNode(trip1, trip1.LabelsBackward);
                    }

                    UnProcessed.Remove(trip1);
                    //dominated rule
                    RemainPateroLabels(ref trip1.LabelsBackward);
                    //判断是否可延伸，即是否 resource-feasible
                    for (i = 0; i < trip1.In_Edges.Count; i++) {
                        Arc arc = trip1.In_Edges[i];
                        trip2 = arc.O_Point;
                        for (j = 0; j < trip1.LabelsBackward.Count; j++) {
                            label1 = trip1.LabelsBackward[j];

                            resource_feasible = false;
                            label2 = REF(label1, trip2, arc);
                            if (resource_feasible)//label1可延伸至trip2
                            {
                                trip2.LabelsBackward.Add(label2);
                            }
                            else { label2 = default(Label); }
                        }
                    }
                }
            }
        }
        void InitializeStartNode() {
            Label oLabel = new Label();
            if (direction == "Forward") {
                UnProcessed[0].LabelsForward.Add(oLabel);
                //initailize(clean) labels of all trips
                for (int i = 1; i < UnProcessed.Count; i++) {
                    UnProcessed[i].LabelsForward.Clear();
                }
            }
            if (direction == "Backward") {
                UnProcessed.Reverse();                
                UnProcessed[0].LabelsBackward.Add(oLabel);
                //initailize(clean) labels of all trips
                for (int i = 1; i < UnProcessed.Count; i++) {
                    UnProcessed[i].LabelsBackward.Clear();
                }
            }
        }
        void InitializeBaseNode(Node trip, List<Label> label_list) {
            foreach (Label label in label_list) {
                foreach (CrewBase crewbase in CrewbaseList) {
                    if (trip.StartStation == crewbase.Station) {
                        label.BaseOfCurrentPath = crewbase;
                        break;
                    }
                }
            }

        }

        Label REF(Label label, Node trip, Arc arc) //在虚拟起终点弧,顺逆向有差别，接续弧是相同的处理
        {
            //首先判断，避免出现 non-elementary label(path),除了基地，其余点不允许在同一Label（path）出现两次
            if (!FitNetworkConstraints(label, trip)) {
                resource_feasible = false;
                return default(Label);
            }

            Label extend = new Label();

            #region 用餐时间窗
            //if (!CheckMealConstraint(label, arc, extend)) {
            /*if (!CheckMealConstraint_v2(label, arc, extend))*/
            if (!CheckMealConstraint_v3(label, arc, extend)) {
                resource_feasible = false;
                //Console.Write("trip1:" + Logger.TripInfoToStr(arc.O_Point));
                //Console.Write("trip2:" + Logger.TripInfoToStr(arc.D_Point));
                //Console.Write(Logger.LabelInfoToStr(label));
                //Console.Write("fixed meal window:[{0},{1}],[{2},{3}]\n",
                //    crew_rules.fixedMealWindow.lunch_start,
                //    crew_rules.fixedMealWindow.lunch_end,
                //    crew_rules.fixedMealWindow.supper_start,
                //    crew_rules.fixedMealWindow.supper_end);
                return default(Label);
            }

            #endregion

            #region  跟据弧的类型计算Label的各项属性值            
            if (arc.ArcType == 1) {
                accumuConsecDrive = label.AccumuConsecDrive + arc.Cost + trip.Length;
                #region //旧版本
                /*
                if (accumuConsecDrive >= (double)crew_rules.MinConsecutiveDriveTime) { 
                    //需要间休                    
                    if (!((int)crew_rules.MinInterval <= arc.Cost && arc.Cost <= (int)crew_rules.MaxInterval))//只有这种情况连接失败：需要间休，但间休时间不满足条件
                    {
                        //connected = false;                        
                        return default(Label);
                    }
                    else {
                        accumuConsecDrive = trip.Length;
                        accumuDrive = label.AccumuDrive + trip.Length;
                        accumuWork = label.AccumuWork + arc.Cost + trip.Length;
                        //C  = label.AccumuCost + arc.Cost - trip.Price;
                    }
                }
                else {
                    accumuConsecDrive = label.AccumuConsecDrive + arc.Cost + trip.Length;
                    accumuDrive = label.AccumuDrive + arc.Cost + trip.Length;
                    accumuWork = label.AccumuWork + arc.Cost + trip.Length;
                    //C  = label.AccumuCost + arc.Cost - trip.Price;
                }
                */
                #endregion

                //需要间休
                //驾驶时间大于最小值时，可以间休也可以不间休，所以对弧的长度没有要求，都可以接续
                //那么若弧长大于最小间休时间，则视为间休，驾驶时间更新为trip.Length，否则视为接续
                if (crew_rules.MinConsecutiveDriveTime <= accumuConsecDrive
                    && accumuConsecDrive < crew_rules.MaxConsecutiveDriveTime) {
                    if (arc.Cost >= crew_rules.MinInterval) {
                        //若弧长满足间休时间，则视为间休
                        accumuConsecDrive = trip.Length;
                        accumuDrive = label.AccumuDrive + trip.Length;
                    }
                    else { //否则，视为接续
                        accumuConsecDrive = label.AccumuConsecDrive + arc.Cost + trip.Length;
                        accumuDrive = label.AccumuDrive + arc.Cost + trip.Length;
                    }

                    accumuWork = label.AccumuWork + arc.Cost + trip.Length;
                }
                else if (accumuConsecDrive < crew_rules.MinConsecutiveDriveTime) {
                    //若驾驶时间小于最小驾驶时间，则不间休
                    accumuConsecDrive = label.AccumuConsecDrive + arc.Cost + trip.Length;
                    accumuDrive = label.AccumuDrive + arc.Cost + trip.Length;
                    accumuWork = label.AccumuWork + arc.Cost + trip.Length;
                }
                else { //若驾驶时间大于最大驾驶时间，则必须间休
                    if (!(crew_rules.MinInterval <= arc.Cost && arc.Cost <= crew_rules.MaxInterval)) {
                        //只有这种情况连接失败：需要间休，但间休时间不满足条件
                        return default(Label);
                    }
                    //必须间休
                    accumuConsecDrive = trip.Length;
                    accumuDrive = label.AccumuDrive + trip.Length;
                    accumuWork = label.AccumuWork + arc.Cost + trip.Length;
                }

            }
            else if (arc.ArcType == 22) //跨天弧，先不和“out”弧合并
            {
                accumuConsecDrive = trip.Length;
                accumuDrive = trip.Length;
                accumuWork = trip.Length;
                //C  = label.AccumuCost + arc.Cost - trip.Price;                                                                            
            }
            else if (arc.ArcType == 20) {
                accumuConsecDrive = trip.Length;
                accumuDrive = trip.Length;
                accumuWork = trip.Length;
            }
            else if (arc.ArcType == 30) {
                accumuConsecDrive = label.AccumuConsecDrive;
                accumuDrive = label.AccumuDrive;
                accumuWork = label.AccumuWork;
            }            
            else { //出退乘
                string taskType = "";
                if ((arc.ArcType == 2 && direction == "Forward") || (arc.ArcType == 3 && direction == "Backward")) { taskType = "out"; }
                if ((arc.ArcType == 3 && direction == "Forward") || (arc.ArcType == 2 && direction == "Backward")) { taskType = "back"; }
                switch (taskType) {
                    case "out":
                        accumuConsecDrive = trip.Length;
                        accumuDrive = trip.Length;
                        accumuWork = trip.Length;                        
                        break;
                    case "back":
                        accumuConsecDrive = label.AccumuConsecDrive;
                        accumuDrive = label.AccumuDrive;
                        accumuWork = label.AccumuWork;                        
                        break;
                    default:
                        taskType = "Exception";
                        break;
                }
            }
            

            SetCostDefinition(label, trip, arc);
            C -= trip.Price;
            //TODO:overed 成本最好还是抽离出去，可以对其进行多种不同的定义，比较哪种优
            #endregion
            //乘务规则
            //if (!(accumuDrive <= crew_rules.PureCrewTime && accumuWork <= crew_rules.TotalCrewTime)) //TODO:是否满足资源约束（还要增添几个乘务规则）

            bool nomal_case = accumuWork > crew_rules.TotalCrewTime;
            //未到终点前，超过了最大值，（任何时候都不能大于最大值）
            bool arrive_base = (arc.ArcType == 3 || arc.ArcType == 22) &&
                 !(crew_rules.PureCrewTime <= accumuWork && accumuWork <= crew_rules.TotalCrewTime);
            //到达终点后，时间不满足要求            

            if (nomal_case || arrive_base) {
                resource_feasible = false;
            }
            else {
                resource_feasible = true;
                extend.AccumuConsecDrive = accumuConsecDrive;
                extend.AccumuDrive = accumuDrive;
                extend.AccumuWork = accumuWork;
                extend.AccumuCost = C;
                extend.PreEdge = arc;
                extend.PreLabel = label;
                extend.BaseOfCurrentPath = label.BaseOfCurrentPath;
                //TODO:新属性
                //以索引来标记trip，可能又会有bug
                if (trip.LineID != 0) {
                    label.VisitedCount.CopyTo(extend.VisitedCount, 0);
                    ++extend.VisitedCount[trip.LineID - 1];
                }
            }

            return extend;
        }
        bool FitNetworkConstraints(Label label, Node trip) {
            if (trip.LineID != 0 && label.VisitedCount[trip.LineID - 1] >= 1) {
                return false; //只能访问一次，elementary path
            }
            if (direction == "Forward") {
                if (trip.Type == 2 && label.BaseOfCurrentPath.Station != trip.EndStation) {
                    return false; //path的起终基地一致
                }
            }
            else if (direction == "Backward") {
                if (trip.Type == 0 && label.BaseOfCurrentPath.Station != trip.StartStation) {
                    return false;
                }
            }

            return true;
        }
        void SetCostDefinition(Label label, Node trip, Arc arc) //TODO
        {
            switch (costType) {
                case 0://求初始解时，用非乘务时间
                    C = label.AccumuCost + arc.Cost;
                    break;
                case 1://全部时间，实际上就是trip的到达时刻。迭代过程中会减去trip.Price，
                    C = label.AccumuCost + arc.Cost + trip.Length;
                    break;
                default:
                    break;
            }
        }

        void RemainPateroLabels(ref List<Label> labelSet) {

            int width = 0;
            int size = labelSet.Count;
            int index = 0;
            int first, last, mid;
            for (width = 1; width < size; width *= 2) {
                for (index = 0; index < (size - width); index += width * 2) {
                    first = index;
                    mid = index + width - 1;
                    //last = index + (width * 2 - 1);//两组相比较，所以是width * 2
                    //last = last >= size ? size - 1 : last;
                    last = Math.Min(index + (width * 2 - 1), size - 1);
                    CheckDominate(ref labelSet, first, mid, last);
                }
            }
            //delete labels which was dominated 
            for (index = 0; index < labelSet.Count; index++) {
                if (labelSet[index].Dominated == true) {
                    labelSet.RemoveAt(index);
                    index--;
                }
            }

        }
        void CheckDominate(ref List<Label> labelSet, int first, int mid, int last) {
            int i, j;
            for (i = first; i <= mid; i++) {
                if (labelSet[i].Dominated) {
                    //labelSet.RemoveAt(i); i--;//先不删，反正最后统一删；现在删了反而不好处理
                    continue;
                }
                for (j = mid + 1; j <= last; j++) {
                    if (labelSet[j].Dominated) {
                        continue;
                    }
                    DominateRule(labelSet[i], labelSet[j]);
                }
            }
        }
        void DominateRule(Label label1, Label label2) {
            if (label1.BaseOfCurrentPath.Station != label2.BaseOfCurrentPath.Station) {
                return;  //所属基地不同，不能比较
            }
            if (label2.curMealStatus != label1.curMealStatus) {
                return; //不同用餐状态，不比较
            }


            if (label1.AccumuCost <= label2.AccumuCost &&
                label1.AccumuConsecDrive <= label2.AccumuConsecDrive &&
                label1.AccumuDrive <= label2.AccumuDrive &&
                label1.AccumuWork <= label2.AccumuWork
                /*&& label1.curMealStatus >= label2.curMealStatus*/) {
                label2.Dominated = true;

            }
            else if (label2.AccumuCost <= label1.AccumuCost &&
                label2.AccumuConsecDrive <= label1.AccumuConsecDrive &&
                label2.AccumuDrive <= label1.AccumuDrive &&
                label2.AccumuWork <= label1.AccumuWork
                /*&& label2.curMealStatus >= label1.curMealStatus*/) {

                label1.Dominated = true;

            }
        }

        /*
        bool CheckMealConstraint(Label curLabel, Arc arc, Label extendLabel) {

            //计算实际可用餐时间窗
            int rmw_min = 0, rmw_max = 0;
            int rmw = 0;
            if (curLabel.curMealStatus == MealStatus.no)
            {   //未用餐
                rmw_min = Math.Max(crew_rules.fixedMealWindows.lunch_start, arc.D_Point.StartTime);
                rmw_max = Math.Min(crew_rules.fixedMealWindows.lunch_end, arc.O_Point.EndTime);                
            }
            else if (curLabel.curMealStatus == MealStatus.lunch)
            {   //用过午餐
                rmw_min = Math.Max(crew_rules.fixedMealWindows.supper_start, arc.D_Point.StartTime);
                rmw_max = Math.Min(crew_rules.fixedMealWindows.supper_end, arc.O_Point.EndTime);
            }
            rmw = rmw_max - rmw_min;

            if (rmw >= crew_rules.MealWindows[0])
            {   //real用餐时间窗 >= 最小用餐时间
                extendLabel.curMealStatus = curLabel.curMealStatus + 1;
            }
            else
            {
                //extendLabel.curMealStatus = curLabel.curMealStatus;
                return false;
            }

            return true;
        }
        */

        bool CheckMealConstraint(Label curLabel, Arc arc, Label extendLabel) {
            /**
             * 总的情况有：
             * 1.未用午餐
             * 2.处于用午餐时间窗“附近”
             * 3.处于午餐后、晚餐前之间
             * 4.处于晚餐时间窗“附近”
             * 5.晚餐后
             * 
             * 要判断是否可用餐，只需要在情况2和情况4的时候再进一步判断是否可以用餐
             * 若不能用餐，则不可行
             * 而在其他时间段，不能用餐的话，也是可行的（因为之后还存在用餐的可能性），只需要把用餐状态保持转移即可
             **/
            int minMealSpan = crew_rules.MealWindows[0];
            FixedMealWindow fmw = crew_rules.fixedMealWindow;

            // 1. 出乘的时候直接可行
            //if ((direction == "Forward" && (arc.ArcType == 2 || arc.ArcType == 20)) ||
            //    (direction == "Backward" && (arc.ArcType == 3 || arc.ArcType == 30))) {
            //    return true;
            //}

            // 检查：
            //（1）若trip横跨tw，则视为在车上用餐，可行
            //（2）            

            //if (arc.D_Point.TrainCode == "G7142/39" || arc.D_Point.TrainCode == "DJ7699") {
            //    int yy = 0;
            //}

            bool lunch_feasible = MealInMealWindow(arc, fmw.lunch_start, fmw.lunch_end, minMealSpan);
            bool supper_feasible = MealInMealWindow(arc, fmw.supper_start, fmw.supper_end, minMealSpan);
            // 检查是否在午/晚餐时间窗“附近”            
            if (lunch_feasible || supper_feasible) {
                if (lunch_feasible) {
                    extendLabel.curMealStatus = MealStatus.lunch;
                }
                if (supper_feasible) {
                    extendLabel.curMealStatus = MealStatus.supper;
                }
            }
            // 检查午餐前、晚餐后、午餐后到晚餐前
            //else if (arc.D_Point.StartTime <= fmw.lunch_start
            //    || arc.O_Point.EndTime >= fmw.supper_end
            //    || (arc.O_Point.EndTime >= fmw.lunch_end && arc.D_Point.StartTime <= fmw.supper_start)
            //    ) {
            else {
                //if (arc.ArcType == 2){ //若当前出乘，可以不满足时间窗)
                //    extendLabel.curMealStatus = MealStatus.lunch;
                //}
                //else if (arc.ArcType == 3 && curLabel.curMealStatus >= MealStatus.lunch) { //若当前退乘了，可以不满足时间窗
                //    extendLabel.curMealStatus = MealStatus.supper;
                //}
                //else {
                    extendLabel.curMealStatus = curLabel.curMealStatus;
                //}
                
            }

            // 只有一种情况是不可行的，即当前遍历点快要超出用餐时间窗最晚限制时，仍未进行最近的用餐
            if ((fmw.lunch_end - arc.O_Point.EndTime <= minMealSpan && extendLabel.curMealStatus < MealStatus.lunch)
               || (fmw.supper_end - arc.O_Point.EndTime <= minMealSpan && extendLabel.curMealStatus < MealStatus.supper)
               ) {
                return false;
            }

            return true;
        }

        /// <summary>        
        /// return true if cur arc has a part in meal time window
        /// return false otherwise
        /// </summary>
        /// <param name="arc"></param>
        /// <param name="fixedMW"></param>
        /// <param name="minMealSpan"></param>
        /// <returns></returns>
        bool MealInMealWindow(Arc arc, int lbMealWindow, int ubMealWindow, int minMealSpan) {
            //if (arc.Cost < minMealSpan) {
            //    return false;
            //}

            int timeLeft = arc.O_Point.EndTime;
            int timeRight = arc.D_Point.StartTime;

            bool lbWindow_in_arcSpan = timeLeft <= lbMealWindow && timeRight - lbMealWindow >= minMealSpan;
            bool ubWindow_in_arcSpan = timeRight >= ubMealWindow && ubMealWindow - timeLeft >= minMealSpan;
            //前两种情况已经表明了acr.length > minMealSpan
            bool arcSpan_in_mealWindow = lbMealWindow <= timeLeft && timeRight <= ubMealWindow && arc.Cost >= minMealSpan;


            int trip_st = arc.D_Point.StartTime;
            int trip_et = arc.D_Point.EndTime;
            // trip横跨tw
            bool trip_covered_tw = //(trip_et- trip_st > ubMealWindow-lbMealWindow)
                (trip_st <= lbMealWindow && trip_et - lbMealWindow >= minMealSpan)
                || (trip_st >= lbMealWindow && trip_et <= ubMealWindow && arc.D_Point.Length >= minMealSpan)
                || (ubMealWindow - trip_st >= minMealSpan && trip_et >= ubMealWindow);

            return lbWindow_in_arcSpan || ubWindow_in_arcSpan || arcSpan_in_mealWindow || trip_covered_tw;
        }

        bool CheckMealConstraint_v2(Label curLabel, Arc arc, Label extendLabel) {
            /**
             * 由于只检查，当arc.D_Point.StartTime > end of meal window 且arc.type == 1时，
             * 向前回溯，无法在最近的那个时间窗内找到可用餐机会
             * 
             **/

            if (arc.ArcType == 1) {
                Node s = arc.O_Point;
                Node t = arc.D_Point;

                int t_end_time = t.EndTime;

                int minMealSpan = crew_rules.MealWindows[0];
                FixedMealWindow fmw = crew_rules.fixedMealWindow;
                                
                // 午餐状态
                // 以trip出时间窗为最极限的情况
                if (curLabel.curMealStatus == MealStatus.no && t_end_time > fmw.lunch_end) {
                    // 先看看当前弧是否可在时间窗内用餐                    
                    if (fmw.lunch_end - arc.O_Point.EndTime >= minMealSpan) {
                        // 当前弧可保证在时间窗内用餐
                        // 更新状态
                        extendLabel.curMealStatus = MealStatus.lunch;
                        return true;
                    }
                    // 若不可行
                    // 回溯，搜索午餐时间窗内是否有用餐机会
                    bool meal_feasible = false;
                    Label temp_label = curLabel;
                    Arc temp_arc = temp_label.PreEdge;
                    while (/*temp_arc.O_Point.EndTime >= fmw.lunch_start*/
                        temp_arc.D_Point.StartTime - fmw.lunch_start >= minMealSpan) {
                        // 判断是否可用餐
                        int real_ub = Math.Min(temp_arc.D_Point.StartTime, fmw.lunch_end);
                        int real_lb = Math.Max(temp_arc.O_Point.EndTime, fmw.lunch_start);
                        if (real_ub - real_lb >= minMealSpan) {
                            meal_feasible = true;
                            // 刷新这之后的label的午餐状态
                            temp_label.curMealStatus = MealStatus.lunch;
                            extendLabel.curMealStatus = MealStatus.lunch;
                            Label first_meal_feasible_label = temp_label;
                            temp_label = curLabel;
                            while (temp_label != first_meal_feasible_label) {
                                temp_label.curMealStatus = MealStatus.lunch;
                                temp_label = temp_label.PreLabel;
                            }
                            break;
                        }
                        temp_label = temp_label.PreLabel;
                        temp_arc = temp_label.PreEdge;
                    }
                    if (!meal_feasible) {
                        return false;
                    }
                }

                // 晚餐状态
                if (curLabel.curMealStatus == MealStatus.lunch && t_end_time > fmw.supper_end) {
                    // 先看看当前弧是否可在时间窗内用餐                    
                    if (fmw.supper_end - arc.O_Point.EndTime >= minMealSpan) {
                        // 当前弧可保证在时间窗内用餐
                        // 更新状态
                        extendLabel.curMealStatus = MealStatus.supper;
                        return true;
                    }
                    // 若不可行
                    // 回溯，搜索午餐时间窗内是否有用餐机会
                    bool meal_feasible = false;
                    Label temp_label = curLabel;
                    Arc temp_arc = temp_label.PreEdge;
                    while (/*temp_arc.O_Point.EndTime >= fmw.supper_start*/
                        temp_arc.D_Point.StartTime - fmw.supper_start >= minMealSpan) {
                        // 判断是否可用餐
                        int real_ub = Math.Min(temp_arc.D_Point.StartTime, fmw.supper_end);
                        int real_lb = Math.Max(temp_arc.O_Point.EndTime, fmw.supper_start);
                        if (real_ub - real_lb >= minMealSpan) {
                            meal_feasible = true;
                            // 刷新这之后的label的午餐状态
                            temp_label.curMealStatus = MealStatus.supper;
                            extendLabel.curMealStatus = MealStatus.supper;
                            Label first_meal_feasible_label = temp_label;
                            temp_label = curLabel;
                            while (temp_label != first_meal_feasible_label) {
                                temp_label.curMealStatus = MealStatus.supper;
                                temp_label = temp_label.PreLabel;
                            }
                            break;
                        }
                        temp_label = temp_label.PreLabel;
                        temp_arc = temp_label.PreEdge;
                    }
                    if (!meal_feasible) {
                        return false;
                    }
                }

                if ((t.StartTime - fmw.lunch_start <= minMealSpan && fmw.lunch_end - t.EndTime <= minMealSpan && curLabel.curMealStatus == MealStatus.no)
                    || (t.StartTime - fmw.supper_start <= minMealSpan && fmw.supper_end - t.EndTime <= minMealSpan && curLabel.curMealStatus <= MealStatus.lunch)
                    ) {
                    return true;
                }


                // 还需检查，是否未用餐，但是已不可能在时间窗内用餐，即arc.D_Point.EndTime > meal_end
                // 注意，为" > "号，可以为" = "，因为之后可能退乘，虽未用餐，但在时间窗内退乘了是可行的，
                // 未用餐退乘的极限情况就是这个"="，一旦超出，表明在时间窗外退乘，且还未用餐

                //!!注意，还有种情况是，trip时间长，甚至横跨时间窗，这是输入的原因，只能视为可行
                if ((curLabel.curMealStatus == MealStatus.no && t.EndTime > fmw.lunch_end) //未用午餐，不能在时间窗内退乘
                    || (curLabel.curMealStatus <= MealStatus.lunch && t.EndTime > fmw.supper_end)//未用晚餐，不能在时间窗内退乘
                    ) {
                    return false;
                }

            }

            return true;
        }

        bool CheckMealConstraint_v3(Label curLabel, Arc arc, Label extendLabel) {
            /**
             * 由于只检查，当arc.D_Point.StartTime > end of meal window 且arc.type == 1时，
             * 向前回溯，无法在最近的那个时间窗内找到可用餐机会
             * 
             **/

            if (arc.ArcType == 30) {
                extendLabel.curMealStatus = curLabel.curMealStatus;
                return true;
            }


            Node s = arc.O_Point;
            Node t = arc.D_Point;

            int t_end_time = t.EndTime;

            int minMealSpan = crew_rules.MealWindows[0];
            FixedMealWindow fmw = crew_rules.fixedMealWindow;

            // 午餐状态
            // 以trip出时间窗为最极限的情况
            if (curLabel.curMealStatus == MealStatus.no && t_end_time > fmw.lunch_end) {
                // 先看看当前弧是否可在时间窗内用餐                    
                if (fmw.lunch_end - arc.O_Point.EndTime >= minMealSpan
                    ||(arc.ArcType == 3 && s.EndTime <= fmw.lunch_end)) {
                    // 当前弧可保证在时间窗内用餐
                    // 更新状态                    
                    extendLabel.curMealStatus = MealStatus.lunch;
                    return true;
                }
                // ！！若当前弧不满足用餐条件
                // 回溯
                bool meal_feasible = false;
                Label temp_label = curLabel;
                Arc temp_arc = temp_label.PreEdge;
                // 1.搜索午餐时间窗内是否有用餐机会
                while (temp_arc.D_Point.StartTime - fmw.lunch_start >= minMealSpan) {
                    // 判断是否可用餐
                    int real_ub = Math.Min(temp_arc.D_Point.StartTime, fmw.lunch_end);
                    int real_lb = Math.Max(temp_arc.O_Point.EndTime, fmw.lunch_start);
                    if (real_ub - real_lb >= minMealSpan) {
                        meal_feasible = true;
                        // 刷新这之后的label的午餐状态
                        temp_label.curMealStatus = MealStatus.lunch;
                        extendLabel.curMealStatus = MealStatus.lunch;
                        Label first_meal_feasible_label = temp_label;
                        temp_label = curLabel;
                        while (temp_label != first_meal_feasible_label) {
                            temp_label.curMealStatus = MealStatus.lunch;
                            temp_label = temp_label.PreLabel;
                        }
                        break;
                    }
                    temp_label = temp_label.PreLabel;
                    temp_arc = temp_label.PreEdge;
                }

                // 由于当前弧不能用餐，而在时间窗内又找不到机会用餐，但可能
                // 出现 arc.D_Point.StartTime - ST > minMealSpan && mealEndTime - arc.D_Point.EndTime < minMealSpan 的情况
                // 表明是因为arc.D_Point运行时间太长，这种情况下，只有当arc为出乘弧时才视为可行
                if (!meal_feasible) {
                    if (temp_arc.D_Point.StartTime - fmw.lunch_start < minMealSpan && fmw.lunch_end - temp_arc.D_Point.EndTime < minMealSpan
                        /*&& temp_arc.ArcType == 2*/) {
                        curLabel.curMealStatus = MealStatus.lunch;
                        extendLabel.curMealStatus = MealStatus.lunch;
                        return true;
                    }
                    
                    return false;
                }
            }

            // 晚餐状态
            if (curLabel.curMealStatus == MealStatus.lunch && t_end_time > fmw.supper_end) {
                // 先看看当前弧是否可在时间窗内用餐
                // 若是退乘弧，则只需满足在时间窗内退乘即可
                if (fmw.supper_end - arc.O_Point.EndTime >= minMealSpan
                    || (arc.ArcType == 3 && s.EndTime <= fmw.supper_end)) {
                    // 当前弧可保证在时间窗内用餐
                    // 更新状态
                    extendLabel.curMealStatus = MealStatus.supper;
                    return true;
                }
                // 若不可行
                // 回溯，搜索午餐时间窗内是否有用餐机会
                bool meal_feasible = false;
                Label temp_label = curLabel;
                Arc temp_arc = temp_label.PreEdge;
                while (/*temp_arc.O_Point.EndTime >= fmw.supper_start*/
                    temp_arc.D_Point.StartTime - fmw.supper_start >= minMealSpan) {
                    // 判断是否可用餐
                    int real_ub = Math.Min(temp_arc.D_Point.StartTime, fmw.supper_end);
                    int real_lb = Math.Max(temp_arc.O_Point.EndTime, fmw.supper_start);
                    if (real_ub - real_lb >= minMealSpan) {
                        meal_feasible = true;
                        // 刷新这之后的label的午餐状态
                        temp_label.curMealStatus = MealStatus.supper;
                        extendLabel.curMealStatus = MealStatus.supper;
                        Label first_meal_feasible_label = temp_label;
                        temp_label = curLabel;
                        while (temp_label != first_meal_feasible_label) {
                            temp_label.curMealStatus = MealStatus.supper;
                            temp_label = temp_label.PreLabel;
                        }
                        break;
                    }
                    temp_label = temp_label.PreLabel;
                    temp_arc = temp_label.PreEdge;
                }
                if (!meal_feasible) {
                    if (temp_arc.D_Point.StartTime - fmw.supper_start < minMealSpan && fmw.supper_end - temp_arc.D_Point.EndTime < minMealSpan
                        /*&& temp_arc.ArcType == 2*/) {
                        curLabel.curMealStatus = MealStatus.supper;
                        extendLabel.curMealStatus = MealStatus.supper;
                        return true;
                    }

                    return false;
                }
            }
            // 还需检查，是否未用餐，但是已不可能在时间窗内用餐，即arc.D_Point.EndTime > meal_end
            // 注意，为" > "号，可以为" = "，因为之后可能退乘，虽未用餐，但在时间窗内退乘了是可行的，
            // 未用餐退乘的极限情况就是这个"="，一旦超出，表明在时间窗外退乘，且还未用餐
            if ((curLabel.curMealStatus == MealStatus.no && t.EndTime > fmw.lunch_end) //未用午餐，不能在时间窗内退乘
                || (curLabel.curMealStatus <= MealStatus.lunch && t.EndTime > fmw.supper_end)//未用晚餐，不能在时间窗内退乘
                ) {
                return false;
            }

            extendLabel.curMealStatus = curLabel.curMealStatus;
            return true;
        }



        public void FindNewPath() {
            List<Node> topoNodeList = UnProcessed;
            Node virD = topoNodeList.Last(); //终点的确定是否可以更加普适（少以Index为索引）
            Label label1;
            Label label2;
            int i;
            //找标号Cost属性值最小的，改变弧长后，Cost即为reduced cost,
            //而主问题的Cj为 reduced cost + sum(trip.price),
            //但在迭代过程中Cj=1440*days
            #region //可利用Linq查询
            //label1 = virD.LabelsForward.Aggregate((l1, l2) => l1.AccumuCost < l2.AccumuCost ? l1 : l2);

            //label1 = (from l in virD.LabelsForward
            //          let minCost = virD.LabelsForward.Max(m => m.AccumuCost)
            //          where l.AccumuCost == minCost
            //          select l).FirstOrDefault();
            #endregion
            label1 = virD.LabelsForward[0];
            for (i = 1; i < virD.LabelsForward.Count; i++) {
                //想当然了！！常规来吧，别想着骚
                //label1 = virD.LabelsForward[i - 1].AccumuCost < virD.LabelsForward[i].AccumuCost ? 
                //         virD.LabelsForward[i - 1] : virD.LabelsForward[i];
                label2 = virD.LabelsForward[i];
                if (label1.AccumuCost > label2.AccumuCost) {
                    label1 = label2;
                }
            }

            //Reduced_Cost           = label1.AccumuCost; //2019-1-27
            New_Column = new Pairing
            {
                ArcList = new List<Arc>()
            };

            int realistic_trip_num = NetWork.num_Physical_trip;//(nodeList.Count - 2) / CrewRules.MaxDays;
            //newAji                 = new int[realistic_trip_num];            
            //for (i = 0; i < realistic_trip_num; i++) { newAji[i] = 0; }
            New_Column.CoverMatrix = new int[realistic_trip_num];
            New_Column.Cost = label1.AccumuCost; //2019-2-1
            New_Column.Coef = 1440;
            //double sum_tripPrice   = 0;
            int pathday = 1;
            Node virO = topoNodeList[0];
            Arc arc;
            arc = label1.PreEdge;
            while (!arc.O_Point.Equals(virO)) {
                New_Column.ArcList.Insert(0, arc);
                //sum_tripPrice += arc.O_Point.Price;
                //newAji[arc.O_Point.LineID - 1] = 1;
                if (arc.O_Point.LineID > 0) {
                    New_Column.CoverMatrix[arc.O_Point.LineID - 1] = 1;//虚拟起终点弧的处理
                }
                label1 = label1.PreLabel;
                arc = label1.PreEdge;
                pathday = arc.ArcType == 22 ? pathday + 1 : pathday;
            }
            New_Column.ArcList.Insert(0, arc);
            New_Column.Coef *= pathday;
        }
        //TODO:添加多列
        public bool FindMultipleNewPColumn(int num_addColumns) {
            List<Node> topoNodeList = UnProcessed;
            Node virD = topoNodeList.Last();
            negetiveLabels = new List<Label>();

            Reduced_Cost = 0;
            Label label1;
            int i;
            //找标号Cost < 0即可           
            for (i = virD.LabelsForward.Count - 1; i >= 0; i--) {
                label1 = virD.LabelsForward[i];
                if (label1.AccumuCost < 0) {
                    negetiveLabels.Add(label1);
                }
            }

            if (negetiveLabels.Count == 0) //检验数均大于0，原问题最优
            {
                return false;
            }

            num_addColumns = Math.Min(num_addColumns, negetiveLabels.Count);
            //TODO:TopN排序，只想最多添加N列，则只需找出TopN即可   
            //先调用方法全部排序吧
            negetiveLabels = negetiveLabels.OrderBy(labelCost => labelCost.AccumuCost).ToList();

            Reduced_Cost = negetiveLabels[0].AccumuCost;//固定为最小Cost
            //reduced_costs = new double[num_addColumns];
            New_Columns = new List<Pairing>(num_addColumns);

            //局部变量                
            int realistic_trip_num = NetWork.num_Physical_trip;
            Node virO = topoNodeList[0];
            Arc arc;
            //newMultiAji = new int[num_addColumns, realistic_trip_num];//全部元素默认为0                

            Report rpt = new Report();
            for (i = 0; i < num_addColumns; i++) {
                label1 = negetiveLabels[i];
                //reduced_costs[i] = label1.AccumuCost;

                New_Column = new Pairing
                {
                    ArcList = new List<Arc>(),
                    CoverMatrix = new int[realistic_trip_num],
                    Cost = label1.AccumuCost,
                    Coef = 1440
                };
                int pathday = 1;

                arc = label1.PreEdge;
                while (!arc.O_Point.Equals(virO)) {
                    New_Column.ArcList.Insert(0, arc);

                    if (arc.O_Point.LineID > 0) {
                        New_Column.CoverMatrix[arc.O_Point.LineID - 1] = 1;
                    }

                    pathday = arc.ArcType == 22 ? pathday + 1 : pathday;

                    arc.D_Point.MealStatus = label1.curMealStatus;


                    label1 = label1.PreLabel;
                    arc = label1.PreEdge;
                }
                New_Column.ArcList.Insert(0, arc);
                New_Column.Coef *= pathday;

                //StringBuilder singlepath = new StringBuilder();
                //rpt.summary_single.SetValue(0, 0, 0, 0);
                //Console.Write(rpt.translate_single_pairing(New_Column,
                //                        ref singlepath, ref rpt.summary_single));

                New_Columns.Add(New_Column);
            }

            return true;

        }
    }

    //调试完毕，没毛病
    public class Topological2
    {
        private Queue<Node> queue;
        //private int[] Indegree;
        private Dictionary<Node, int> Indegree;

        public List<Node> Order;//拓扑序列

        /// <summary>
        /// Network , strat point
        /// </summary>
        /// <param name="net"></param>
        /// <param name="s"></param>
        public Topological2(NetWork net, Node s) {
            List<Node> nodeset = net.NodeSet;
            Node trip;
            queue = new Queue<Node>();
            //Indegree = new int[net.NodeSet.Count - 1];
            Indegree = new Dictionary<Node, int>();

            Order = new List<Node>();
            for (int i = 0; i < nodeset.Count; i++) {
                trip = nodeset[i];
                Indegree[trip] = trip.In_Edges.Count;
            }
            //Indegree[Indegree.Length - 1] = net.NodeSet[Indegree.Length - 1].In_Edges.Count;
            //for (int v = 0; v < net.NodeSet.Count; v++)
            //{
            //    foreach (var a in net.ArcSet)
            //    {
            //        if (a.D_Point.ID == net.NodeSet[v].ID)
            //        {
            //            Indegree[v]++;
            //        }
            //    }
            //}
            foreach (Node node in nodeset) {
                if (node.In_Edges.Count == 0) {
                    queue.Enqueue(node);
                }
            }

            int count = 0;
            while (queue.Count != 0) {
                Node Top = queue.Dequeue();
                int top = Top.ID;
                Order.Add(Top);
                foreach (var arc in net.ArcSet) {
                    if (arc.O_Point == Top && arc.D_Point.ID != 1) //终点在最后
                    {
                        if (--Indegree[arc.D_Point] == 0) {
                            queue.Enqueue(arc.D_Point);
                        }
                    }
                }
                count++;
            }
            Order.Add(nodeset[1]);//终点
            count++;
            if (count != nodeset.Count) { throw new Exception("此图有环"); }

        }
    }
}
