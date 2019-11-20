using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

using common_db;

namespace CG_CSP_1440
{

    class TestMain {
        static void Main(string[] args)
        {
            Test test = new Test();
            test.RunTest();
        }
    }

    public class Test
    {

        public string[] TestInstances = { "京津", "沪杭", "BigScale", "BigScale2" };
        public Dictionary<string, int> InstancePreSetCrewNum = new Dictionary<string, int>()
        {
            {"京津", 60},
            {"沪杭", 240},
            {"BigScale", 300},
            {"BigScale2", 400}
        };

        public FixedMealWindow[] FixedMealWindowsAry =
        {
            new FixedMealWindow(660, 780, 1020, 1140), //2h
            new FixedMealWindow(630, 810, 990, 1170), //3h
            new FixedMealWindow(600, 840, 960, 1200), //4h
            new FixedMealWindow(4000, 8400, 9600, 14400) //INF
            //new FixedMealWindows(3000, 7200, 7210, 28800)            
        };
        private List<int[]> net_crew_params = new List<int[]>() {
            new int[11]{13, 15, 40, 180, 250, 180, 250,3400,3900,1440,1 },//京津城际实例
            new int[11]{13, 13, 90, 240, 250, 240, 250,3400,3900,1440,1 },
            new int[11]{6, 6, 200, 180, 720, 180, 720,3400,3900,1440,1 },
            new int[11]{6, 6, 400, 120, 780, 120, 780,3400,3900,1440,1 }
        };
        //net.CreateT_S_S_NetWork(180, 250, 40, 15, 3312, 3330, 3400, 3900, 180, 250);//京津城际实例
        //net.CreateT_S_S_NetWork(240, 540, 90, 13, 3312, 3300, 3400, 3900, 240, 540);//沪宁杭
        //net.CreateT_S_S_NetWork(180, 720, 200, 6, 3312, 3330, 600, 2800, 180, 720);//bigscale
        //net.CreateT_S_S_NetWork(120, 780, 400, 6, 3312, 3330, 540, 2880, 120, 780);//bigscale2


        public void RunTest()
        {
            for (int caseIndex = 0; caseIndex < 1/*TestInstances.Length*/; caseIndex++) {
                string caseName = TestInstances[caseIndex];
                string data_dir = @"\data\" + caseName;
                DataReader Data = new DataReader();

                List<string> csvfiles;
                Data.Connect_csvs(out csvfiles, data_dir);
                //Data.LoadRules_csv(); //该函数并未考虑时间窗
                for (int i = 0; i < net_crew_params.Count; i++) {
                    Data.CrewRules = new CrewRules(net_crew_params[caseIndex][i++],
                        net_crew_params[caseIndex][i++], net_crew_params[caseIndex][i++],
                        net_crew_params[caseIndex][i++], net_crew_params[caseIndex][i++],
                        net_crew_params[caseIndex][i++], net_crew_params[caseIndex][i++],
                        net_crew_params[caseIndex][i++], net_crew_params[caseIndex][i++],
                        net_crew_params[caseIndex][i++], net_crew_params[caseIndex][i++]);
                }
                CrewRules.All_Num_Crew = InstancePreSetCrewNum[caseName];

                Data.CrewRules.MealWindows = new int[2] { 30, 40 };
                for (int windowIndex = 0; windowIndex < 1/*FixedMealWindowsAry.Length*/; windowIndex++) {
                    string case_internal_name = "case" + (caseIndex + 1) + (windowIndex + 1);
                    caseName  = TestInstances[caseIndex];
                    caseName += "\\" + case_internal_name;

                    Data.CrewRules.fixedMealWindow = FixedMealWindowsAry[windowIndex];                    

                    Console.WriteLine("**********START TEST CASE [{0}] CONSIDERING MEAL TIME WINDOW [{1}]", caseName, windowIndex);
                    Stopwatch timer = new Stopwatch();
                    timer.Start();
                    Data.CrewRules.DisplayRules();

                    Data.LoadData_csv(Data.CrewRules.MaxDays); //create nodes，实际可以放在外层循环，但为了统计时间，放在这里 

                    TestInstance(caseName, Data);

                    timer.Stop();
                    Console.WriteLine("total time spended in solve this case is {0} s", timer.Elapsed.TotalSeconds);
                    Console.WriteLine("**********END TEST CASE [{0}] CONSIDERING MEAL TIME WINDOW [{1}]", caseName, windowIndex);
                }                
            }
        }

        public void TestInstance(string caseName, DataReader Data)
        {
            Stopwatch sw_all = new Stopwatch();
            Stopwatch sw_creat_net = new Stopwatch();
            Stopwatch sw_IS = new Stopwatch();
            Stopwatch sw_BandB = new Stopwatch();

            sw_all.Start();
            sw_creat_net.Start();
            sw_IS.Start();

            NetWork Net = new NetWork();
            Net.CreateNetwork(Data);
            Net.IsAllTripsCovered();
            sw_creat_net.Stop();
            //检查无误
            //get initail solution
            InitialSolution IS = new InitialSolution(Net);
            //IS.GetFeasibleSolutionByPenalty();
            //IS.GetFeasibleSolutionByMethod1();
            IS.GetVirtualPathSetAsInitSoln();
            sw_IS.Stop();
            //Report Report_IS = new Report(IS.PathSet);
            //Console.WriteLine(Report_IS.TransferSolution());
            //Console.WriteLine("平均纯乘务时间：{0} 平均换乘时间{1} 平均task数{2}", Report_IS.summary_mean.mean_PureCrew,
            //    Report_IS.summary_mean.mean_Trans, Report_IS.summary_mean.mean_Tasks);
            
            //string initial_solution_dir = System.Environment.CurrentDirectory + "\\结果\\" + caseName + "\\初始解.txt";            
            //Report_IS.WriteCrewPaths(initial_solution_dir);
            //Console.WriteLine(sw_IS.Elapsed.TotalSeconds);
                        
            /*********<<下面开始测试CG>>***************************************************************************/
            sw_BandB.Start();
            CSP csp = new CSP(Net);
            csp.testCase = caseName;
            csp.Branch_and_Price(IS);
            sw_BandB.Stop();
            sw_all.Stop();

            Logger.GetUncoveredTasks(csp.GetOptPathSet(), Net.TripList, caseName);
            Logger.GetSchedule(csp.GetOptPathSet(), caseName);


            string path = System.Environment.CurrentDirectory + "\\结果\\" + caseName + "\\求解信息.txt";
            FileStream fs = new FileStream(path, FileMode.Create);
            StreamWriter strwrite = new StreamWriter(fs);
            strwrite.WriteLine("建网时间:{0}", sw_creat_net.Elapsed.TotalSeconds);
            strwrite.WriteLine("接续弧的数量:{0}", Net.ArcSet.Count());
            strwrite.WriteLine("网络中节点数量:{0}", Net.NodeSet.Count());
            //strwrite.WriteLine("初始解产生时间:{0}", sw_IS.Elapsed.TotalSeconds);
            strwrite.WriteLine("LP_OPT_SOLN时间:{0}", csp.time_root_node);
            strwrite.WriteLine("分支定界用时:{0}", sw_BandB.Elapsed.TotalSeconds);
            strwrite.WriteLine("总乘务组数量:{0}", csp.num_all_crew);
            strwrite.WriteLine("模型总共求解时间:{0}", sw_all.Elapsed.TotalSeconds);
            strwrite.WriteLine("总目标函数:{0}", /*csp.OBJVALUE);*/csp.All_obj);
            strwrite.WriteLine("最短路总计算次数:{0}", csp.total_nums_rcspp);
            strwrite.WriteLine("初始解列数:{0}", IS.PathSet.Count);
            strwrite.WriteLine("列池总数:{0}", csp.ColumnPool.Count);
            strwrite.Close();
            fs.Close();

            // get objective value in the process of iterations
            StreamWriter obj_iter = new StreamWriter(System.Environment.CurrentDirectory + "\\结果\\" + caseName + "\\OBJ迭代.csv");
            obj_iter.WriteLine("ObjValue");
            foreach (var obj in csp.obj_iter)
            {
                obj_iter.WriteLine(obj);
            }
            obj_iter.Close();

            //string num_label_iter = System.Environment.CurrentDirectory + "\\结果\\" + caseName + "\\标号数量与求解时间迭代(2).csv";
            //strwrite = new StreamWriter(num_label_iter, false, Encoding.Default);
            //strwrite.WriteLine("iter,label num,time*10000");
            //for (int i = 0; i < csp.num_label_iter.Count; i++)
            //{
            //    strwrite.WriteLine(i + 1 + "," + csp.num_label_iter[i] + "," + csp.time_rcspp[i] * 10000);
            //}
            //strwrite.Close();            

        }

    }
}