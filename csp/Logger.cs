using System;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using CG_CSP_1440;
//namespace CG_CSP_1440
//{
//}
namespace common_db
{
    class Logger
    {
        public static string TripInfoToStr(Node trip)
        {
            StringBuilder strbu = new StringBuilder();
            strbu.AppendFormat("trip info: [{0},{1},{2},{3},{4}]\n"
                ,trip.TrainCode
                ,trip.StartTime, trip.EndTime
                , trip.StartStation, trip.EndStation);
            return strbu.ToString();
        }
        public static string LabelInfoToStr(Label label){            
            StringBuilder partial_path = new StringBuilder();
            Label pre_label = label;
            Arc pre_arc = label.PreEdge;
            Stack<string> content = new Stack<string>();
            while (pre_arc != null)
            {
                content.Push(pre_arc.D_Point.TrainCode);
                pre_label = pre_label.PreLabel;
                pre_arc = pre_label.PreEdge;
            }
            partial_path.Append("partial path:[");
            while (content.Count > 1)
            {
                partial_path.AppendFormat("{0}->", content.Pop());
            }
            if (content.Count == 1) {
                partial_path.AppendFormat("{0}]", content.Pop());
            }
            else
            {
                partial_path.AppendFormat("]");
            }

            int endtime = label.PreEdge == null ? 0 : label.PreEdge.D_Point.EndTime;


            StringBuilder strbu = new StringBuilder();
            strbu.AppendFormat("label info: crewBase[{0}],curMealStatus[{1}],DPoint_ET[{2}]\n"
                , label.BaseOfCurrentPath.Station
                , label.curMealStatus
                , endtime);

            strbu.AppendLine(partial_path.ToString());
            
            return strbu.ToString();
        }

        public static void GetUncoveredTasks(List<Pairing> soln, List<Node> taskSet, string caseName, string mealWindow) {
            
            List<Node> coveredTasks = new List<Node>();
            foreach (var pairing in soln) {
                foreach (var arc in pairing.ArcList) {
                    if (arc.O_Point.Type == 1) {
                        coveredTasks.Add(arc.O_Point);
                    }
                }
            }
            var uncoveredTasks = taskSet.Except(coveredTasks);

            string path = System.Environment.CurrentDirectory + "\\结果\\" + caseName + "\\" + mealWindow + "\\";
            String fileName = string.Format("uncoveredTasks_{0}_{1}_{2}_CG.txt", uncoveredTasks.Count(), caseName, mealWindow);
            StreamWriter uncoveredTasksFile = new StreamWriter(path + fileName, false, Encoding.Default);
            uncoveredTasksFile.WriteLine("--Uncovered Tasks("+ uncoveredTasks.Count() + ")");
            foreach (var task in uncoveredTasks) {
                uncoveredTasksFile.Write(Logger.tripToStr(task));
            }
            uncoveredTasksFile.Close();
        }

        public static void GetSchedule(List<Pairing> soln, string caseName, string mealWindow) {
            string path = System.Environment.CurrentDirectory + "\\结果\\" + caseName + "\\" + mealWindow + "\\";
            String fileName = string.Format("scheduleForVisualize_{0}_{1}_{2}_CG.csv", caseName, mealWindow, soln.Count);
            StreamWriter scheduleFile = new StreamWriter(path + fileName, false, Encoding.UTF8);
            scheduleFile.WriteLine("交路编号,编号,车次,出发时刻,到达时刻,出发车站,到达车站");

            soln.Sort(PairingContentASC.pairingASC);

            int pathID = 0;
            string str = "";
            foreach (var pairing in soln) {
                str = "";
                ++pathID;
                foreach (var arc in pairing.ArcList) {                    
                    if (arc.O_Point.Type == 1) {
                        str += pathID + "," + tripToStr(arc.O_Point);
                    }
                }
                scheduleFile.Write(str);
            }

            scheduleFile.Close();
        }

        private static string tripToStr(Node trip) {
            StringBuilder str = new StringBuilder();

            str.AppendFormat("{0},{1},{2},{3},{4},{5}\n",
                trip.ID,
                trip.TrainCode,
                trip.StartTime,
                trip.EndTime,
                trip.StartStation,
                trip.EndStation);

            return str.ToString();
        }
        /// <summary>
        /// 统计一个case多个时间窗的结果
        /// </summary>
        /// <param name="caseName"></param>
        /// <param name="caseSummaries"></param>
        public static void GetSummary(/*string caseName, */List<Summary> caseSummaries) {
            string path = System.Environment.CurrentDirectory + "\\结果\\";//+ caseName + "\\";
            String fileName = string.Format("caseSummaries_{0}_CG.txt", caseSummaries.Count()/*, caseName*/);
            StreamWriter summaryFile = new StreamWriter(path + fileName, false, Encoding.Default);
            summaryFile.WriteLine("case,algorithm,solve_time,ub,lb,gap,pairing_num");

            foreach (var single_case_summary in caseSummaries) {
                summaryFile.Write(single_case_summary.toStrLine());
            }
            summaryFile.Close();
        }

    }

    /// <summary>
    /// 记录一个case的求解方面的信息
    /// </summary>
    public class Summary {
        string _algorithmName = "default";
        public string AlgorithmName {
            get { return _algorithmName; }
        }
        string _caseNameFull = "caseName_mealWindow";
        public string CaseNameFull {
            get { return _caseNameFull; }
        }
        double _solveTime = 0;
        public double SolveTime {
            get { return _solveTime; }
        }
        double _objValue_LB = -1;
        public double LB {
            get { return _objValue_LB; }
        }
        double _objValue_UB = -1;
        public double UB {
            get { return _objValue_UB; }
        }
        double _gap = 100;
        public double GAP {
            get { return _gap; }
        }
        int _pairingNum = 0;
        public int PairingNum {
            get { return _pairingNum; }
        }

        public Summary(string algorithmName, string caseName_mealWindow, double solveTime, double LB, double UB, int pairingNum) {
            _algorithmName = algorithmName;
            _caseNameFull = caseName_mealWindow;
            _solveTime = solveTime;
            _objValue_LB = LB;
            _objValue_UB = UB;
            _gap = ((_objValue_UB - _objValue_LB) / _objValue_UB) * 100;
            _pairingNum = pairingNum;
        }
        public string toStrLine() {
            StringBuilder strBuilder = new StringBuilder();
            strBuilder.AppendFormat("{0},{1},{2},{3},{4},{5},{6}\n",
                _caseNameFull,
                _algorithmName,
                _solveTime,
                _objValue_UB,
                _objValue_LB,
                _gap.ToString("f2"),
                _pairingNum);
            return strBuilder.ToString();
        }

    }
}

