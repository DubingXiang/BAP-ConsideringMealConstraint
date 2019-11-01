using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CG_CSP_1440
{

    public struct FixedMealWindow
    {
        public FixedMealWindow(int lunchStart, int lunchEnd, int supperStart, int supperEnd)
        {
            lunch_start = lunchStart;
            lunch_end = lunchEnd;
            supper_start = supperStart;
            supper_end = supperEnd;
        }
        public int lunch_start;
        public int lunch_end;
        public int supper_start;
        public int supper_end;
    }
    public class CrewRules
    {
        #region 乘务规则参数设置,初始解，最短路中的参数 = 该处，则只需在建网时更改参数
        //public static int minTransTime = 20;        
        //public static int[] Interval = new int[2] { 30, 120 };
        ////public enum Interval { min = 30, max = 120 }
        ////public static int ConsecuDrive = 180;//铭-240;岩-180
        //public enum ConsecuDrive { min = 180, max = 360 }
        //public static int PureCrewTime = 550;//京津360same //等价于哲铭中的minDayCrewTime
        //public static int TotalCrewTime = 660;//铭-540;岩-480 //等价于哲铭中的maxDayCrewTime
        ////public static int NonBaseRest = 960;//铭-720;岩-960
        //public enum NonBaseRest
        //{
        //    min = 720,
        //    max = 1200  //设置时间窗，为了减少弧
        //}
        //public static int MaxLongDuties = 1440;//先一样
        //public static int MaxDays = 2;
        #endregion
        private int min_transtime;
        private int min_interval;
        private int max_interval;
        private int min_consecutive_drive_time;
        private int max_consecutive_drive_time;
        private int pure_crew_time;
        private int total_crew_time;
        private int min_nonbase_rest;
        private int max_nonbase_rest;
        private int max_long_duties;
        private int max_days;        
        //
        public static int All_Num_Crew;        

        public int MinTranstime {
            get { return min_transtime; }
            set { min_transtime = value; }
        }
        
        public int MinInterval {
            get { return min_interval; }
            set { min_interval = value; }
        }
        
        public int MaxInterval {
            get { return max_interval; }
            set { max_interval = value; }
        }
        
        public int MinConsecutiveDriveTime {
            get { return min_consecutive_drive_time; }
            set { min_consecutive_drive_time = value; }
        }
        
        public int MaxConsecutiveDriveTime {
            get { return max_consecutive_drive_time; }
            set { max_consecutive_drive_time = value; }
        }
        
        public int PureCrewTime {
            get { return pure_crew_time; }
            set { pure_crew_time = value; }
        }
        
        public int TotalCrewTime {
            get { return total_crew_time; }
            set { total_crew_time = value; }
        }
        
        public int MinNonBaseRest {
            get { return min_nonbase_rest; }
            set { min_nonbase_rest = value; }
        }
        
        public int MaxNonBaseRest {
            get { return max_nonbase_rest; }
            set { max_nonbase_rest = value; }
        }
        
        public int MaxLongDuties {
            get { return max_long_duties; }
            set { max_long_duties = value; }
        }
        
        public int MaxDays {
            get { return max_days; }
            set { max_days = value; }
        }

        public int[] MealWindows = new int[2];
       
        public FixedMealWindow fixedMealWindow;// = new FixedMealWindows();

        public CrewRules() { }
        public CrewRules(string file) 
        {
            Dubin_Data.CSVReader rule_csv = new Dubin_Data.CSVReader(file);
            Dictionary<string, List<string>> parameters = rule_csv.Read(new char[1] { ',' });
            MinTranstime = Convert.ToInt32(parameters["最短换乘时间"][0]);
            MinInterval = Convert.ToInt32(parameters["间休时间窗"][0]);
            MaxInterval = Convert.ToInt32(parameters["间休时间窗"][1]);
            MinConsecutiveDriveTime = Convert.ToInt32(parameters["连续驾驶时间窗"][0]);
            MaxConsecutiveDriveTime = Convert.ToInt32(parameters["连续驾驶时间窗"][1]);
            MinNonBaseRest = Convert.ToInt32(parameters["外驻时间窗"][0]);
            MaxNonBaseRest = Convert.ToInt32(parameters["外驻时间窗"][1]);
            MaxLongDuties = Convert.ToInt32(parameters["交路允许长度"][0]);
            MaxDays = Convert.ToInt32(parameters["持续天数"][0]);

            MealWindows[0] = Convert.ToInt32(parameters["用餐时间窗"][0]);
            MealWindows[1] = Convert.ToInt32(parameters["用餐时间窗"][1]);

            fixedMealWindow = new FixedMealWindow(Convert.ToInt32(parameters["午餐时间窗"][0]),
                Convert.ToInt32(parameters["午餐时间窗"][1]),
                Convert.ToInt32(parameters["晚餐时间窗"][0]),
                Convert.ToInt32(parameters["晚餐时间窗"][1]));

        }
        public CrewRules(int min_transtime,
            int min_interval, int max_interval,
            int min_consecutive_drive_time, int max_consecutive_drive_time,
            int pure_crew_time, int total_crew_time,
            int min_nonbase_rest, int max_nonbase_rest,
            int max_long_duties, int max_days,
            int min_meal_window, int max_meal_window,
            int lunchStart, int lunchEnd, int supperStart, int supperEnd) 
        {
            this.min_transtime = min_transtime;
            this.min_interval = min_interval;
            this.max_interval = max_interval;
            this.min_consecutive_drive_time = min_consecutive_drive_time;
            this.max_consecutive_drive_time = max_consecutive_drive_time;
            this.pure_crew_time = pure_crew_time;
            this.total_crew_time = total_crew_time;
            this.min_nonbase_rest = min_nonbase_rest;
            this.max_nonbase_rest = max_nonbase_rest;
            this.max_long_duties = max_long_duties;
            this.max_days = max_days;

            this.MealWindows[0] = min_meal_window;
            this.MealWindows[1] = max_meal_window;

            fixedMealWindow.lunch_start = lunchStart;
            fixedMealWindow.supper_end = lunchEnd;
            fixedMealWindow.supper_start = supperStart;
            fixedMealWindow.supper_end = supperEnd;
        }

        public void SetMealWindows(int min_meal_window, int max_meal_window,
            int lunchStart, int lunchEnd, int supperStart, int supperEnd)
        {
            this.MealWindows[0] = min_meal_window;
            this.MealWindows[1] = max_meal_window;

            fixedMealWindow.lunch_start = lunchStart;
            fixedMealWindow.supper_end = lunchEnd;
            fixedMealWindow.supper_start = supperStart;
            fixedMealWindow.supper_end = supperEnd;
        }

        public void DisplayRules() 
        {
            String all_rules = String.Format("min_transtime: {0}\nmin_interval: {1}\t       max_interval: {2}\n" +
                                       "min_consecutive_drive_time: {3}\t max_consecutive_drive_time: {4}\n" +
                                       "pure_crew_time: {5}\t          total_crew_time: {6}\n" +
                                       "min_nonbase_rest: {7}\t          max_nonbase_rest: {8}\n" +
                                       "max_long_duties: {9}\t        max_days: {10}" +
                                       "min_meal_window: {11}\t    max_meal_window: {12}"+
                                       "fixedMealWindows = [{13},{14} | {15},{16}]\n",
                                       min_transtime, min_interval, max_interval, min_consecutive_drive_time, max_consecutive_drive_time,
                                       pure_crew_time, total_crew_time, min_nonbase_rest, max_nonbase_rest, max_long_duties, max_days,
                                       this.MealWindows[0], this.MealWindows[1],
                                       fixedMealWindow.lunch_start, fixedMealWindow.lunch_end,
                                       fixedMealWindow.supper_start, fixedMealWindow.supper_end);

            Console.WriteLine("CURRENT PARAMETERS OF CREWRULES IS \n" + all_rules);
        }
    }
    
    public class NetWork
    {
        public  List<Node> NodeSet;
        public List<Node> TripList;
        public List<Arc> ArcSet;
        static int num_physical_trip;
        public static int num_Physical_trip 
        {
            get { return num_physical_trip; }            
        }
        public CrewRules CrewRules;
        #region //乘务规则参数设置, 只需在建网时更改参数
        //int TransTime           = CrewRules.minTransTime;
        ////int minInterval         = (int)CrewRules.Interval.min;
        //int maxInterval         = (int)CrewRules.Interval.max;
        ////int minConsecuDrive     = (int)CrewRules.ConsecuDrive.min;//铭-240;岩-180
        ////int maxConsecuDrive     = (int)CrewRules.ConsecuDrive.max;
        //int PureCrewTime        = CrewRules.PureCrewTime;//same
        //int TotalCrewTime       = CrewRules.TotalCrewTime;//铭-540;岩-480
        //int minNonBaseRest      = (int)CrewRules.NonBaseRest.min;//铭-720;岩-960
        //int maxNonBaseRest      = (int)CrewRules.NonBaseRest.max;
        //int Longest             = CrewRules.MaxLongDuties;//先一样
        //int MaxDays             = CrewRules.MaxDays;
        #endregion        

        public void CreateNetwork(DataReader Data) 
        {
            //DataReader Data = new DataReader();
            ////Data.Ds = Data.ConnSQL(ConnStr);
            ////Data.LoadData_sql(Data.Ds, MaxDays);
            //List<string> csvfiles;
            //Data.Connect_csvs(out csvfiles, ConnStr);
            //Data.LoadRules_csv();
            //Data.LoadData_csv(MaxDays);
            CrewRules = Data.CrewRules;
            //建弧
            this.NodeSet = Data.NodeSet;
            this.TripList = Data.TripList;
            ArcSet = new List<Arc>();
            int i, j;
            Node trip1, trip2;
            int length = 0;
            Node virO = NodeSet[0];
            Node virD = NodeSet[1];
            
            List<Node> odBase = Data.ODBaseList;
            for (i = 0; i < odBase.Count; i++) //虚拟起终点弧
            {
                //virO--OBase
                if (odBase[i].Type == 0) 
                {
                    Arc arc = new Arc();
                    arc.O_Point = virO;
                    arc.D_Point = odBase[i];
                    arc.Cost = 0;     //NodeSet[i].StartTime;
                    arc.ArcType = 20; //1-接续弧，2-出乘弧，3-退乘弧, 20-虚拟起点弧，30-虚拟终点弧
                    ArcSet.Add(arc);
                    virO.Out_Edges.Add(arc);
                    odBase[i].In_Edges.Add(arc);
                }
                else if (odBase[i].Type == 2) 
                {
                    Arc arc = new Arc();
                    arc.O_Point = odBase[i];
                    arc.D_Point = virD;
                    arc.Cost = 0;
                    arc.ArcType = 30;
                    ArcSet.Add(arc);
                    odBase[i].Out_Edges.Add(arc);
                    virD.In_Edges.Add(arc);
                }
            }    
        
            for (i = 2; i < NodeSet.Count; i++) 
            {
                trip1 = NodeSet[i];               
                for (j = 2; j < NodeSet.Count; j++)
                {
                    trip2 = NodeSet[j];

                    if (trip1.Type == 1 && trip2.Type == 1) //接续弧
                    {
                        length = trip2.StartTime - trip1.EndTime;
                        if (trip1 != trip2 && trip1.EndStation == trip2.StartStation && length > 0)
                        {
                            //if ((trip1.RoutingID == trip2.RoutingID) ||
                            //    (trip1.RoutingID != trip2.RoutingID &&
                            //    ((TransTime <= length && length <= Interval[1]) ||
                            //    (trip2.StartTime > 1440 && trip1.EndTime < 1440 && minNonBaseRest <= length && length <= maxNonBaseRest))))
                            if (trip1.RoutingID == trip2.RoutingID || Transferable(trip1, trip2, length))//顺序不能变，因为是通过逻辑转换才将上面的简化为这样
                            {
                                Arc arc = new Arc();
                                arc.O_Point = trip1;
                                arc.D_Point = trip2;
                                arc.Cost = length;
                                arc.ArcType = length >= CrewRules.MinNonBaseRest ? 22 : 1;//22-跨天了
                                ArcSet.Add(arc);
                                trip1.Out_Edges.Add(arc);
                                trip2.In_Edges.Add(arc);
                            }
                        }
                    }
                    else if (trip1.EndStation == trip2.StartStation && trip1.Type == 0 && trip2.Type == 1)
                    {                        
                       
                            Arc arc = new Arc();
                            arc.O_Point = trip1;
                            arc.D_Point = trip2;
                            arc.Cost = trip2.StartTime;
                            arc.ArcType = 2;
                            ArcSet.Add(arc);
                            trip1.Out_Edges.Add(arc);
                            trip2.In_Edges.Add(arc);                                                                        
                    }
                    else if (trip1.EndStation == trip2.StartStation && trip1.Type == 1 && trip2.Type == 2)
                    {
                        Arc arc = new Arc();
                        arc.O_Point = trip1;
                        arc.D_Point = trip2;
                        int d = (trip1.EndTime / 1440 + 1);
                        arc.Cost = 1440 * d - trip1.EndTime;
                        arc.ArcType = 3;
                        ArcSet.Add(arc);
                        trip1.Out_Edges.Add(arc);
                        trip2.In_Edges.Add(arc);
                    }      
                }                                
            }
            //原始区段数
            num_physical_trip = (NodeSet.Count - 2 - 2 * DataReader.CrewBaseList.Count) / CrewRules.MaxDays;

            #region CheckTestArcs
            //foreach (Arc arc in ArcSet) 
            //{
            //    Console.WriteLine(arc.O_Point.ID + " -> " + arc.D_Point.ID + " type: " + arc.ArcType);
            //}
            #endregion
            //删去出度或入度为0的点与弧
            DeleteUnreachableNodeandEdge(ref TripList);            
        }
        bool Transferable(Node trip1, Node trip2, int length)
        {
            bool sameDay = false;
            bool differentDay = false;
            if(CrewRules.MinTranstime <= length)
            {              
                if (trip1.EndTime < 1440 && trip2.StartTime > 1440)
                {
                    differentDay = CrewRules.MinNonBaseRest <= length && length <= CrewRules.MaxNonBaseRest;
                }
                else 
                {
                    sameDay = length <= CrewRules.MaxInterval;
                }
            }            
            return sameDay || differentDay;
        }

        void DeleteUnreachableNodeandEdge(ref List<Node> TripList) 
        {
            int i = 0, j, k;
            Arc edge1, edge2;
            Node trip1, trip2;
            for (i = 0; i < TripList.Count; i++)
            {
                trip1 = TripList[i];
                if (trip1.Out_Edges.Count == 0 || trip1.In_Edges.Count == 0)
                {
                    #region//删去 出度 = 0 的点的 In_Edges
                    for (j = 0; j < trip1.In_Edges.Count; j++)
                    {
                        edge1 = trip1.In_Edges[j];
                        trip2 = edge1.O_Point;
                        for (k = 0; k < trip2.Out_Edges.Count; k++)
                        {
                            edge2 = trip2.Out_Edges[k];
                            if (edge1 == edge2)
                            {
                                trip2.Out_Edges.RemoveAt(k);
                                break;//只可能有一条，所以找到了删去后，不用再继续寻找，减少了搜索次数
                            }
                        }
                        //上面这个for循环改为：
                        //if (trip2.Out_Edges.Contains(edge1)) 
                        //{
                        //    trip2.Out_Edges.Remove(edge1);
                        //}

                        for (k = 0; k < ArcSet.Count; k++)
                        {
                            edge2 = ArcSet[k];
                            if (edge1 == edge2)
                            {
                                ArcSet.RemoveAt(k);
                                break;
                            }
                        }
                    }
                    #endregion
                    #region//删去 入度 = 0的点的 Out_Edges
                    for (j = 0; j < trip1.Out_Edges.Count; j++)
                    {
                        edge1 = trip1.Out_Edges[j];
                        trip2 = edge1.D_Point;
                        for (k = 0; k < trip2.In_Edges.Count; k++)
                        {
                            edge2 = trip2.In_Edges[k];
                            if (edge1 == edge2)
                            {
                                trip2.In_Edges.RemoveAt(k); k--;
                                break;
                            }
                        }
                        for (k = 0; k < ArcSet.Count; k++)
                        {
                            edge2 = ArcSet[k];
                            if (edge1 == edge2)
                            {
                                ArcSet.RemoveAt(k); k--;
                                break;
                            }
                        }
                    }
                    #endregion
                    TripList.RemoveAt(i);
                    NodeSet.Remove(trip1);
                    i--;
                }
            }
        
        }

        public void IsAllTripsCovered() 
        {
            List<int> copy_LinesID = CopyLinesID(TripList);
            List<int> unCoveredTrips = new List<int>();
            try
            {
                FindUncoveredTrips(copy_LinesID,out unCoveredTrips);
            }
            catch (TripUncoveredException ex)
            {
                Console.WriteLine("{0}\n检查乘务基地设置或复制天数是否有误", ex.Message);
                OutUncoveredTrips(unCoveredTrips);
            }            
        }
        List<int> CopyLinesID(List<Node> TripList)
        {
            List<int> copy_LineIDs = new List<int>();
            for (int i = 0; i < TripList.Count; i++)//TODO:排除虚拟起终点。但是应该没影响，因为下面加了判断
            {
                if (!copy_LineIDs.Contains(TripList[i].LineID))//因为考虑复制，所以实际只需有一天的点
                {
                    copy_LineIDs.Add(TripList[i].LineID);
                }
            }
            return copy_LineIDs;
        }
        void FindUncoveredTrips(List<int> copy_LinesID, out List<int> unCoveredTrips) 
        {
            unCoveredTrips = new List<int>();
            int i = 0;
            for (i = 1; i <= num_Physical_trip; i++)
            {
                if (!copy_LinesID.Contains(i))
                {
                    unCoveredTrips.Add(i);
                    //break;//只加break更注重只要存在uncovered点就行，不需知道是哪个点uncovered，因而信息不明确
                }
            }
            if (unCoveredTrips.Count > 0) {
                throw new TripUncoveredException("存在未被覆盖的点!");
            }

            //return unCoveredTrips;
        }
        void OutUncoveredTrips(List<int> unCoveredTrip) {
            Console.WriteLine("uncovered trips: ");
            foreach (int trip in unCoveredTrip) {
                Console.Write(trip + ", ");
            }
        }

        public class TripUncoveredException : ApplicationException
        {
            //const string Message = "存在未被覆盖的点";
            public TripUncoveredException() { }
            public TripUncoveredException(string message) : base(message) { }
        }
    }

}
