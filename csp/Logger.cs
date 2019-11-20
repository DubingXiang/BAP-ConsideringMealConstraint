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

        public static void GetUncoveredTasks(List<Pairing> soln, List<Node> taskSet,string testCase) {
            string path = System.Environment.CurrentDirectory + "\\结果\\" + testCase;
            StreamWriter uncoveredTasksFile = new StreamWriter(path + "\\uncoveredTasksFile.txt", false, Encoding.Default);
            
            List<Node> coveredTasks = new List<Node>();
            foreach (var pairing in soln) {
                foreach (var arc in pairing.ArcList) {
                    if (arc.O_Point.Type == 1) {
                        coveredTasks.Add(arc.O_Point);
                    }
                }
            }

            var uncoveredTasks = taskSet.Except(coveredTasks);
            uncoveredTasksFile.WriteLine("--Uncovered Tasks("+ uncoveredTasks.Count() + ")");
            foreach (var task in uncoveredTasks) {
                uncoveredTasksFile.Write(Logger.TripInfoToStr(task));
            }
            uncoveredTasksFile.Close();
        }

        public static void GetSchedule(List<Pairing> soln, string testCase) {
            string path = System.Environment.CurrentDirectory + "\\结果\\" + testCase;
            StreamWriter scheduleFile = new StreamWriter(path + "\\scheduleForVirtualize.csv", false, Encoding.UTF8);
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

    }
}

