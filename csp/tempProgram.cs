using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace CG_CSP_1440
{
    class tempProgram
    {
        static void Main(string[] args) 
        {
            //string data_path = "Server = PC-201606172102\\SQLExpress;DataBase = 乘务计划;Integrated Security = true";
            //string data_path = "Server = PC-201606172102\\SQLExpress;DataBase = 乘务计划CSP1440;Integrated Security = true";
            // string data_dir = @"\data\京津";
            Stopwatch sw_all = new Stopwatch();
            Stopwatch sw_creat_net = new Stopwatch();
            Stopwatch sw_IS = new Stopwatch();
            Stopwatch sw_BandB = new Stopwatch();

            sw_all.Start();
            string test_case = "沪杭";//"BigScale";////
            CrewRules.All_Num_Crew = 240;//京津-60；沪杭-240；bigscal-300;bigscal2-400
            string data_dir = @"\data\" + test_case;//京津";
            DataReader Data = new DataReader();

            List<string> csvfiles;
            Data.Connect_csvs(out csvfiles, data_dir);
            Data.LoadRules_csv(); //该函数并未考虑时间窗
            //考虑时间窗的话，为了测试方便，直接在代码里设置时间窗的参数，而不是放在文件中

            Data.CrewRules.DisplayRules();
            Data.LoadData_csv(Data.CrewRules.MaxDays);

            sw_creat_net.Start();
            NetWork Net = new NetWork();
            Net.CreateNetwork(Data); 
            Net.IsAllTripsCovered();
            sw_creat_net.Stop();

            sw_IS.Start();
            //检查无误
            InitialSolution IS = new InitialSolution(Net);
            //IS.GetFeasibleSolutionByPenalty();
            IS.GetFeasibleSolutionByMethod1();//顺逆向标号在多基地时有问题：如对点i，顺向时最短路对应基地为B1,逆向时最短路对应基地为B2.错误
            sw_IS.Stop();

            Report Report_IS = new Report(IS.PathSet);
            Console.WriteLine(Report_IS.TransferSolution());
            Console.WriteLine("平均纯乘务时间：{0} 平均换乘时间{1} 平均task数{2}" , Report_IS.summary_mean.mean_PureCrew , 
                Report_IS.summary_mean.mean_Trans , Report_IS.summary_mean.mean_Tasks);
            //string initial_solution_dir = System.Environment.CurrentDirectory + "\\结果\\京津\\初始解.txt";
            string initial_solution_dir = System.Environment.CurrentDirectory + "\\结果\\" + test_case + "\\初始解.txt";
            //Report_IS.WriteCrewPaths(@"D:\代码项目\C#\CG-version2.1_cost_1440-重构\CG_CSP_1440\jj158ISS_LineList.txt");
            Report_IS.WriteCrewPaths(initial_solution_dir);
            Console.WriteLine(sw_IS.Elapsed.TotalSeconds);
            //checked：OK
            /*********<<下面开始测试CG>>***************************************************************************/
            sw_BandB.Start();
            CSP csp = new CSP(Net);
            csp.testCase = test_case;
            csp.Branch_and_Price(IS);
            sw_BandB.Stop();
            sw_all.Stop();

            StreamWriter obj_iter = new StreamWriter(System.Environment.CurrentDirectory + "\\结果\\" + test_case + "\\OBJ迭代.csv");
            obj_iter.WriteLine("ObjValue");
            foreach (var obj in csp.obj_iter)
            {
                obj_iter.WriteLine(obj);
            }
            obj_iter.Close();
          

            string path = System.Environment.CurrentDirectory + "\\结果\\" + test_case + "\\求解信息.txt";
            FileStream fs = new FileStream(path , FileMode.Create);
            StreamWriter strwrite = new StreamWriter(fs);
            strwrite.WriteLine("建网时间:{0}" , sw_creat_net.Elapsed.TotalSeconds);
            strwrite.WriteLine("接续弧的数量:{0}" , Net.ArcSet.Count());
            strwrite.WriteLine("网络中节点数量:{0}" , Net.NodeSet.Count());
            strwrite.WriteLine("初始解产生时间:{0}" , sw_IS.Elapsed.TotalSeconds);            
            strwrite.WriteLine("分支定界用时:{0}" , sw_BandB.Elapsed.TotalSeconds);
            strwrite.WriteLine("总乘务组数量:{0}" , csp.num_all_crew);
            strwrite.WriteLine("模型总共求解时间:{0}" , sw_all.Elapsed.TotalSeconds);
            strwrite.WriteLine("总目标函数:{0}", /*csp.OBJVALUE);*/csp.All_obj);
            strwrite.WriteLine("最短路总计算次数:{0}", csp.total_nums_rcspp);
            strwrite.WriteLine("初始解列数:{0}", IS.PathSet.Count);
            strwrite.WriteLine("列池总数:{0}", csp.ColumnPool.Count);
            strwrite.Close();
            fs.Close();

            string dualprice_iter = System.Environment.CurrentDirectory + "\\结果\\" + test_case + "\\对偶乘子迭代.csv";
            strwrite = new StreamWriter(dualprice_iter, false, Encoding.Default);
            //strwrite.WriteLine("iter,dual_price");
            csp.task_dualPrice.OrderBy(k => k.Key).ToDictionary(k => k.Key, v => v.Value);
            int i = 0;
            for (i = 1; i < csp.task_dualPrice.Keys.Count; i++)
            {
                strwrite.Write("task" + i + ",");
            }
            strwrite.WriteLine("task" + i);
            for (int j = 0; j < csp.task_dualPrice[1].Count; j++)
            {
                for (i = 1; i < csp.task_dualPrice.Keys.Count; i++)
                {
                    strwrite.Write(csp.task_dualPrice[i][j] + ",");
                }
                strwrite.WriteLine(csp.task_dualPrice[i][j]);
            }
            strwrite.Close();

            string num_label_iter = System.Environment.CurrentDirectory + "\\结果\\" + test_case + "\\标号数量与求解时间迭代(2).csv";
            strwrite = new StreamWriter(num_label_iter, false, Encoding.Default);
            strwrite.WriteLine("iter,label num,time*10000");
            for (i = 0; i < csp.num_label_iter.Count; i++)
            {
                strwrite.WriteLine(i + 1 + "," + csp.num_label_iter[i] +","+ csp.time_rcspp[i]*10000);
            }
            strwrite.Close();

            //string time_rcspp_iter = System.Environment.CurrentDirectory + "\\结果\\" + test_case + "\\最短路求解时间迭代.csv";
            //strwrite = new StreamWriter(time_rcspp_iter, false, Encoding.Default);
            //strwrite.WriteLine("iter,time");
            //for (i = 0; i < csp.time_rcspp.Count; i++)
            //{
            //    strwrite.WriteLine(i+1 + "," );
            //}
            //strwrite.Close();

        }
    }

    public class Report 
    {
        StringBuilder pathStr;
        List<Pairing> pathset;

        public struct SummarySingleDuty 
        {
            public double totalLength,
            totalConnect,
            pureCrewTime,
            externalRest,
                accumuwork;

            public void SetValue(double length, double connect, double pure_crewTime, double external_rest) 
            {
                totalLength = length;
                totalConnect = connect;
                pureCrewTime = pure_crewTime;
                externalRest = external_rest;
                accumuwork = totalLength - externalRest ;
            }
        }
        public SummarySingleDuty summary_single = new SummarySingleDuty();
        public struct Summary_Mean 
        {
            public double mean_PureCrew,
            mean_Trans,
            mean_Tasks;

            public void SetValue(double pureTime, double transTime, double tasks) 
            {
                mean_PureCrew = pureTime;
                mean_Trans = transTime;
                mean_Tasks = tasks;
            }
        }
        public Summary_Mean summary_mean = new Summary_Mean();
        public struct Summary_Algotithm 
        {
            public double appear_time_FirstFeasibleSolution,
            GAP_FirstFeasibleSolution,
            GAp_Opt,
            total_TreeNodes,
            total_Columns;
        }
        public Report() 
        {
            pathStr = new StringBuilder();
        }

        public Report(List<Pairing> PathSet) 
        {
            pathStr = new StringBuilder();
            pathset = PathSet;
        }

        public StringBuilder TransferSolution()
        {            
            int pathindex = 0;
            int sum_duties = 0;

            summary_mean.SetValue(0, 0, 0);

            foreach (Pairing path in pathset)
            {
                ++pathindex;
                summary_single.SetValue(0, 0, 0, 0);
                
                //start_time = 0; end_time = 0; num_external_days = 0;
                pathStr.AppendFormat("乘务交路{0}: ", pathindex);

                translate_single_pairing(path, ref pathStr, ref summary_single);
                //TODO:计算平均值
                sum_duties += Convert.ToInt32(path.Coef / 1440);
                summary_mean.mean_PureCrew += summary_single.pureCrewTime;
                summary_mean.mean_Trans += summary_single.totalConnect - summary_single.externalRest;
                summary_mean.mean_Tasks += path.Arcs.Count - 3;
            }
            cal_MeanSummary(sum_duties, ref summary_mean);

            return pathStr;
        }

        public StringBuilder translate_single_pairing(Pairing path, 
            ref StringBuilder pathStr, 
            ref SummarySingleDuty summary) 
        {
            double start_time = 0, end_time = 0;
            int num_external_days = 0;

            foreach (Arc arc in path.Arcs)
            {
                switch (arc.ArcType)
                {
                    case 2:
                        pathStr.AppendFormat("{0}站{1}分出乘", arc.O_Point.StartStation, arc.D_Point.StartTime);
                        start_time = arc.D_Point.StartTime;
                        break;
                    case 1:
                        pathStr.AppendFormat("{0} {1}", arc.O_Point.TrainCode, "→");
                        summary.totalConnect += arc.Cost;
                        break;
                    case 22:
                        pathStr.AppendFormat("{0} {1}", arc.O_Point.TrainCode, "→");
                        summary.totalConnect += arc.Cost;
                        summary.externalRest += arc.Cost;
                        num_external_days++;
                        break;
                    case 3:
                        pathStr.AppendFormat("{0} {1}站{2}分退乘", arc.O_Point.TrainCode, arc.D_Point.EndStation, arc.O_Point.EndTime);
                        end_time = arc.O_Point.EndTime;
                        break;
                    default:
                        break;
                }
            }
            summary.totalLength = end_time - start_time;
            summary.pureCrewTime = summary.totalLength - summary.totalConnect;
            summary.accumuwork = summary.totalLength - summary.externalRest +120 ;

            pathStr.AppendFormat(" ,总长度, {0},纯乘务时间 {1},总接续时间 {2},外驻时间 {3},总工作时间{4}",
                summary.totalLength, summary.pureCrewTime, summary.totalConnect, summary.externalRest,summary.accumuwork);
            
            pathStr.AppendLine();            
            return pathStr;
        }

        void cal_MeanSummary(int sum_duties, ref Summary_Mean s_mean) 
        {
            s_mean.mean_PureCrew /= sum_duties;
            s_mean.mean_Trans /= sum_duties;
            s_mean.mean_Tasks /= sum_duties;
        }

        public void WriteCrewPaths(string file)
        {
            StreamWriter Crew_paths = new StreamWriter(file,false);

            TransferSolution();
            Crew_paths.WriteLine(this.pathStr);

            Crew_paths.Close();


        }

    }
}
