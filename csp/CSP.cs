using System;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using ILOG.Concert;
using ILOG.CPLEX;

using common_db;


namespace CG_CSP_1440
{
    class CSP
    {
        public double OBJVALUE; 
        public List<Pairing> OptColumnSet;
        public List<Pairing> ColumnPool = new List<Pairing>();
        public Cplex masterModel;

        public int num_all_crew;
        public double All_obj;
        public double time_root_node = 0;

        double lb_LR = 0;
        //< 与LR形式相同的LB
        public double LB_LR {
            get { return lb_LR; }
        }
        double ub_LR = 0;
        //< 与LR形式相同的UB
        public double UB_LR {
            get { return ub_LR; }
        }

        //task在迭代过程中对应的对偶价格
        public Dictionary<int, List<double>> task_dualPrice = new Dictionary<int, List<double>>();
        public int total_nums_rcspp=0; //子问题总调用次数
        public List<double> time_rcspp = new List<double>(); //每次子问题的求解时间，最后可以用来求均值
        Stopwatch rcspT = new Stopwatch();
        public List<int> num_label_iter = new List<int>();
        public string testCase;
        public List<double> obj_iter = new List<double>();
       

        //参数
        int num_AddColumn = 50;
        double GAP = 0.01;

        private List<Pairing> PathSet;

        public List<Pairing> GetOptPathSet() { return PathSet; }

        private List<double> CoefSet;//Cj
        private List<int[]> A_Matrix;//aji
        private List<INumVar> DvarSet;

        private List<INumVar> virtualVarSet;

        int initialPath_num;
        int trip_num;
        int realistic_trip_num;
        NetWork Network;
        List<Node> NodeSet;
        List<Node> TripList;

        IObjective Obj;
        IRange[] Constraint;

        //至始至终都只有一个实体，求解过程中只是改变各属性值
        RCSPP R_C_SPP;
        TreeNode root_node;
        List<int> best_feasible_solution; //元素值为1的为决策变量的下标。只记录一个可行解，不管有多个目标值相同的解        
        
        #region //废弃auxiliary slack variables
        //ArrayList ExtraCovered ;
        //ArrayList Uncovered;
        //double[] Penalty;
        //double[] U;        
        #endregion

        public CSP(NetWork network) 
        {
            this.Network = network;
            NodeSet = network.NodeSet;
            TripList = network.TripList;
            Topological2 topo;
            R_C_SPP = new RCSPP(this.Network, out topo);

            masterModel = new Cplex();
            masterModel.SetParam(Cplex.Param.Simplex.Display, 0);
            masterModel.SetParam(Cplex.Param.MIP.Display, 0);

            //added 4-30
            foreach (var trip in network.TripList)
            {
                if (task_dualPrice.ContainsKey(trip.LineID) == false) 
                {
                    task_dualPrice.Add(trip.LineID, new List<double>());
                }
                //task_dualPrice[trip.LineID].Add(0);
            }

        }

        /// <summary>分支定价
        /// 从指定的初始解作为上界开始
        /// </summary>
        /// <param name="IS"></param>
        public void Branch_and_Price(InitialSolution IS) 
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            Build_RMP(IS);

            root_node = new TreeNode();
            CG(ref root_node);

            sw.Stop();
            time_root_node = sw.Elapsed.TotalSeconds;
            Console.WriteLine("根节点求解时间： " + sw.Elapsed.TotalSeconds);

            best_feasible_solution = new List<int>();                        
            //RecordFeasibleSolution(root_node, ref best_feasible_solution);
            RecordFeasibleSolution(ref best_feasible_solution);
            double UB = IS.initial_ObjValue;//int.MaxValue;
            double LB = root_node.obj_value;

            sw.Restart();

            Branch_and_Bound(root_node, LB, UB);

            #region 转为mip
            /*
            foreach (var x in DvarSet) {
                x.LB = 0;
                x.UB = 1;
            }
            
            IConversion mip = masterModel.Conversion(DvarSet.ToArray(), NumVarType.Int);
            masterModel.Add(mip);
            masterModel.Solve();
            Console.WriteLine("**************MIP_OBJ: {0}**************", masterModel.GetObjValue());
            RecordFeasibleSolution(ref best_feasible_solution);
            string path = System.Environment.CurrentDirectory + "\\结果\\" + testCase; 
            StreamWriter feasible_solutions = new StreamWriter(path + "\\mipSoln.csv", false, Encoding.Default);
            Report repo = new Report();
            int num = 1;
            int start_cost = 1;
            double sum_start_cost = 0;
            double sum_accumuworktime = 0;

            List<Pairing> optSoln = new List<Pairing>();
            for(int i = 0; i < DvarSet.Count; i++) {
                var x = DvarSet[i];
                if (masterModel.GetValue(x) > 0) {
                    
                    feasible_solutions.WriteLine("乘务交路 " + (num++) + " ");
                    StringBuilder singlepath = new StringBuilder();
                    repo.summary_single.SetValue(0, 0, 0, 0);                    
                    feasible_solutions.Write(repo.translate_single_pairing(ColumnPool[i],
                                            ref singlepath, ref repo.summary_single));
                    feasible_solutions.WriteLine();
                    sum_accumuworktime += repo.summary_single.accumuwork;
                    sum_start_cost += start_cost++;

                    optSoln.Add(ColumnPool[i]);
                }
            }
            feasible_solutions.Close();

            //masterModel.WriteSolution(testCase + "\\_mip.sln");
            //masterModel.ExportModel(testCase + "\\_mip.LP");
            
            num_all_crew = --num;
            Console.WriteLine("num_all_crew " + num_all_crew);
            //Console.WriteLine("ALL_obj" + All_obj);
            All_obj = 1440 * num;
            All_obj += sum_accumuworktime +  Get_Stop_cost(num);//+ sum_start_cost;
            Console.WriteLine("sum_start_cost = {0}\nsum_stop_cost = {1}", sum_start_cost, Get_Stop_cost(num));
            */
            #endregion

            
            Console.WriteLine("num_all_crew:" + num_all_crew);
            Console.WriteLine("ALL_obj:" + All_obj);
            
            sw.Stop();
            Console.WriteLine("分支定界共花费时间：" + sw.Elapsed.TotalSeconds);
        }
        /// <summary>建立最初的RMP
        /// 依据初始解
        /// </summary>
        /// <param name="IS"></param>
        public void Build_RMP(InitialSolution IS)
        {
            initialPath_num = IS.PathSet.Count;
            trip_num = TripList.Count;
            realistic_trip_num = NetWork.num_Physical_trip;

            DvarSet = new List<INumVar>();
            CoefSet = new List<double>();
            A_Matrix = new List<int[]>();
            PathSet = new List<Pairing>();

            //virtualVarSet = new List<INumVar>();
            //for (int virtual_x = 0; virtual_x < realistic_trip_num; virtual_x++) {
            //    virtualVarSet.Add(masterModel.NumVar(0, 1, NumVarType.Float));
            //}

            CoefSet = IS.Coefs;
            A_Matrix = IS.A_Matrix;
            PathSet = IS.PathSet;

            foreach (var p in PathSet)
            {
                ColumnPool.Add(p);
            }

            int i, j;
            Obj = masterModel.AddMinimize();
            Constraint = new IRange[realistic_trip_num];
            /**按行建模**/
            //vars and obj function
            for (j = 0; j < initialPath_num; j++)
            {
                INumVar var = masterModel.NumVar(0, 1, NumVarType.Float);
                DvarSet.Add(var);
                Obj.Expr = masterModel.Sum(Obj.Expr, masterModel.Prod(CoefSet[j], DvarSet[j]));
            }
            //constraints
            for (i = 0; i < realistic_trip_num; i++)
            {
                INumExpr expr = masterModel.NumExpr();

                for (j = 0; j < initialPath_num; j++)
                {
                    expr = masterModel.Sum(expr,
                                           masterModel.Prod(A_Matrix[j][i], DvarSet[j]));//在从初始解传值给A_Matrix，已经针对网络复制作了处理
                }

                Constraint[i] = masterModel.AddGe(expr, 1);
            }
        }        

        #region 分支定界
        /// <summary>嵌入列生成的分支定界
        /// 中间会保存产生的可行解们到指定文件
        /// </summary>
        /// <param name="root_node"></param>
        /// <param name="LB"></param>
        /// <param name="UB"></param>
        public void Branch_and_Bound(TreeNode root_node, double LB, double UB) 
        {            
            string path = System.Environment.CurrentDirectory + "\\结果\\" + testCase + "\\B_a_P_soln.txt";

            //TODO:中间可行解存放文件，传参
            StreamWriter feasible_solutions = new StreamWriter(path, false, Encoding.Default);
            Report repo = new Report();
            int num_iter = 0;
            
            bool continue_backtrack = false;
            int max_backtrack = 15;

            while (true)
            {                
                #region //先判断可行，再比较目标函数大小
                /*
                if (Feasible(root_node) == false)
                {
                    if (root_node.obj_value > UB) //不必在该点继续分支
                    {
                        Backtrack(ref root_node.fixed_vars);                                              
                    }
                    else //root_node.obj_value <= UB,有希望，更新下界，继续分支,
                    {
                        LB = root_node.obj_value;                        
                    }   
                }
                else //可行，更新上界。【2-24-2019：只要可行，不管可行解是否优于当前最优可行（OBJ < UB）都不用在该点继续分支，而是回溯】
                {                    
                    if (root_node.obj_value <= UB) //root_node.obj_value <= UB，更新UB，停止在该点分支，回溯
                    {
                        UB = root_node.obj_value;
                        RecordFeasibleSolution(root_node, ref best_feasible_solution);

                        if ((UB - LB) / UB < GAP) //这也是停止准则，但只能放在这里
                        {                            
                            break;
                        }
                    }

                    Backtrack(ref root_node.fixed_vars); 
                }

                SolveChildNode(ref root_node);
                */
                #endregion
                Console.WriteLine("第{0}个结点，OBJ为{1}", num_iter + 1, root_node.obj_value);

                #region 先比较界限，再判断是否可行
                if (root_node.obj_value > UB) //不论可行与否，只要大于上界，都必须剪枝，然后回溯
                {                    
                    Backtrack(ref root_node);                    
                }
                else if (LB <= root_node.obj_value && root_node.obj_value <= UB) 
                {
                    if (Feasible(root_node) == false) //不可行，更新下界；继续向下分支
                    {
                        LB = root_node.obj_value;
                    }
                    else //可行，更新上界，记录当前可行解；回溯
                    {
                        Console.WriteLine("可行节点");
                        UB = root_node.obj_value;
                        //RecordFeasibleSolution(root_node, ref best_feasible_solution);
                        RecordFeasibleSolution(ref best_feasible_solution);
                        PathSet.Clear();
                        
                        #region 可行解输出至文件
                        
                        feasible_solutions.WriteLine("第{0}个节点", num_iter);
                        feasible_solutions.WriteLine("UB = {0}, LB = {1}, GAP = {2}", UB, LB, (UB - LB) / UB);
                        int num = 1;
                        //int start_cost = 1;
                        //double sum_start_cost = 0;
                        double sum_accumuworktime = 0;

                        #region //last version
                        /*
                        foreach (var index in root_node.fixing_vars)
                        {
                            //num += 1;
                            feasible_solutions.WriteLine("乘务交路 " + (num++) + " ");
                            StringBuilder singlepath=new StringBuilder();
                            repo.summary_single.SetValue(0,0,0,0);                            
                            feasible_solutions.Write(repo.translate_single_pairing(ColumnPool[index],
                                                    ref singlepath, ref repo.summary_single));
                            //feasible_solutions.WriteLine();
                            sum_accumuworktime += repo.summary_single.accumuwork;
                            sum_start_cost += start_cost++;

                            PathSet.Add(ColumnPool[index]);
                        }
                        foreach(var p in root_node.fixed_vars)
                        {
                            if(masterModel.GetValue(DvarSet[p]) == 1)
                            {
                                //num += 1;
                                feasible_solutions.WriteLine("乘务交路 " + (num++) + " ");
                                StringBuilder singlepath = new StringBuilder();
                                repo.summary_single.SetValue(0 , 0 , 0 , 0);
                                feasible_solutions.Write(repo.translate_single_pairing(ColumnPool[p] ,
                                                        ref singlepath , ref repo.summary_single));
                                //feasible_solutions.WriteLine();
                                sum_accumuworktime += repo.summary_single.accumuwork;
                                sum_start_cost += start_cost++;

                                PathSet.Add(ColumnPool[p]);
                            }
                          
                        }
                        foreach (var k_v in root_node.not_fixed_var_value_pairs)
                        {
                            if (k_v.Value > 0)
                            {
                                //num += 1;
                                feasible_solutions.WriteLine("乘务交路 " + (num++) + " ");
                                StringBuilder singlepath = new StringBuilder();
                                repo.summary_single.SetValue(0, 0, 0, 0);
                                
                                feasible_solutions.Write(repo.translate_single_pairing(ColumnPool[k_v.Key],
                                                        ref singlepath, ref repo.summary_single));
                                //feasible_solutions.WriteLine();
                                sum_accumuworktime += repo.summary_single.accumuwork;
                                sum_start_cost += start_cost++;

                                PathSet.Add(ColumnPool[k_v.Key]);
                            }
                        }
                        */
                        #endregion

                        double[] dvar_values = masterModel.GetValues(DvarSet.ToArray());
                        for (int i = 0; i < dvar_values.Length; i++) {
                            if (dvar_values[i] > 0 && ColumnPool[i].ArcList.Count > 0) {
                                PathSet.Add(ColumnPool[i]);

                                //ColumnPool[i].LR_Price = 1440 + ColumnPool[i].ArcList.Last().O_Point.EndTime - ColumnPool[i].ArcList[0].D_Point.StartTime;
                                ColumnPool[i].SetTripList();
                                ColumnPool[i].calLRPrice();

                                sum_accumuworktime += ColumnPool[i].LR_Price;
                            }
                        }

                        PathSet.Sort(PairingContentASC.pairingASC);
                        foreach (var pairing in PathSet) {
                            feasible_solutions.WriteLine("乘务交路 " + (num++) + " ");
                            StringBuilder singlepath = new StringBuilder();
                            repo.summary_single.SetValue(0, 0, 0, 0);
                            feasible_solutions.Write(repo.translate_single_pairing(pairing,
                                                    ref singlepath, ref repo.summary_single));
                        }

                        #endregion

                        

                        num_all_crew = --num;
                        int sum_vir_remain_cost = (int)Get_Stop_cost(num);                        
                        All_obj += sum_accumuworktime + sum_vir_remain_cost;
                        ub_LR = All_obj;

                        Console.WriteLine("num_all_crew " + num_all_crew);
                        Console.WriteLine("ALL_OBJ_VALUE: " + All_obj);
                        //Console.WriteLine("sum_start_cost = {0}\nsum_stop_cost = {1}", sum_start_cost, sum_vir_remain_cost);
                        continue_backtrack = true;
                        Backtrack(ref root_node);
                    }
                }
                else if (root_node.obj_value < LB) //TODO：这种情况不知道是否会出现
                {
                    LB = root_node.obj_value;
                }
                //避免在同一节点多次回溯，因为实际之后的回溯都是越来越差的，所以提前结束
                if (continue_backtrack) 
                {
                    if (--max_backtrack < 0) 
                    {
                        feasible_solutions.Close();
                        break;
                    }
                }

                Branch(ref root_node); //寻找需要分支的变量

                if (TerminationCondition(root_node, UB, LB) == true)
                {
                    feasible_solutions.Close();
                    break;
                }
                num_iter++;
                
                CG(ref root_node); //求解子节点
                #endregion
            }
    
        }
        public double Get_Stop_cost(int num_crew)
        {            
            double sum_stop_cost = 0;
            for(int i = num_crew + 1 ; i <= CrewRules.All_Num_Crew ; i++)
            {
                sum_stop_cost += 1000;
            }
            return sum_stop_cost;
        }

        /// <summary>停止准则
        /// 1.达到设定的GAP 2.所有变量均已分支过
        /// 还可以添加其他的
        /// </summary>
        /// <param name="tree_node"></param>
        /// <param name="UB"></param>
        /// <param name="LB"></param>
        /// <returns></returns>
        bool TerminationCondition(TreeNode tree_node, double UB, double LB) 
        {
            return MetGAP(UB, LB) || NoVarBranchable(tree_node);
        }
        bool NoVarBranchable(TreeNode node)
        {
            /*没有节点（变量）可以回溯，所有变量分支过了
                           * 没有变量可以分支，所有变量都分支过了
                            */
            if (root_node.fixing_vars.Count == 0
                || root_node.not_fixed_var_value_pairs.Count == 0)
            {
                Console.WriteLine("找不到可分支的变量");
                return true;
            }
            return false;
        }
        bool MetGAP(double UB, double LB) 
        {
            return (UB - LB) < UB * GAP;
        }

        /// <summary>判断是否是可行解
        ///每个变量均为整数{0,1}
        /// </summary>
        /// <param name="tree_node"></param>
        /// <returns></returns>
        bool Feasible(TreeNode tree_node) 
        {
            //foreach (var var_value in tree_node.not_fixed_var_value_pairs)             
            //{
            //    if (ISInteger(var_value.Value) == false) 
            //    {
            //        return false;
            //    }    
            //}
            foreach (var x in DvarSet) {
                if (ISInteger(masterModel.GetValue(x)) == false) {
                    return false;
                }
            }

            return true;
        }
        /// <summary>判断"value"是否为整数
        /// 为了普遍性.（本问题决策变量是0-1变量）
        /// </summary>
        /// <param name="value"></param>
        /// <param name="epsilon"></param>
        /// <returns></returns>
        bool ISInteger(double value, double epsilon = 1e-10) 
        {
            return Math.Abs(value - Convert.ToInt32(value)) <= epsilon; //不是整数，不可行（浮点型不用 == ，!=比较）           
        }

        /// <summary>回溯 + 剪枝
        /// 已分支过的变量不得再分支
        /// </summary>
        /// <param name="tree_node"></param>
        void Backtrack(ref TreeNode tree_node) 
        {
            if (tree_node.fixing_vars.Count == 0) {
                return;
            }
            //剪枝
            tree_node.fixed_vars.Add(tree_node.fixing_vars.Last());

            tree_node.fixing_vars.RemoveAt(tree_node.fixing_vars.Count - 1);
        }
        /// <summary>选择分支变量
        /// value最大（最接近1）的变量（Index）
        /// </summary>
        /// <param name="tree_node"></param>
        void Branch(ref TreeNode tree_node)
        {
            //找字典最大Value对应的key
            /*用lambda： var k = node.not_fixed_var_value_pairs.FirstOrDefault(v => v.Value.Equals
                                                                            *(node.not_fixed_var_value_pairs.Values.Max()));*/

            int var_of_max_value = tree_node.not_fixed_var_value_pairs.First().Key;
            double max_value = tree_node.not_fixed_var_value_pairs.First().Value;

            foreach (var var_value in tree_node.not_fixed_var_value_pairs) 
            {
                var_of_max_value = max_value > var_value.Value ? var_of_max_value : var_value.Key;
                max_value = max_value > var_value.Value ? max_value : var_value.Value;
            }

            tree_node.fixing_vars.Add(var_of_max_value);
            tree_node.not_fixed_var_value_pairs.Remove(var_of_max_value);
        }

        /// <summary>记录当前最优解
        /// value = 1 的dvar
        /// </summary>
        /// <param name="best_feasible_solution"></param>
        void RecordFeasibleSolution(ref List<int> best_feasible_solution) 
        {
            best_feasible_solution.Clear();
            double value;
            for (int i = 0; i < DvarSet.Count; i++)
            {
                value = masterModel.GetValue(DvarSet[i]);
                //判断其变量是否为 1（为不失一般性，写的函数功能是判断是否为整数）
                if (Convert.ToInt32(value) > 0 && ISInteger(value))
                {
                    best_feasible_solution.Add(i);
                }
            }
        }

        //void RecordFeasibleSolution(TreeNode node, ref List<int> best_feasible_solution)
        //{
        //    best_feasible_solution.Clear(); //找到更好的可行解了，不需要之前的了            
        //    ///方式2
        //    //已分支的变量固定为 1，直接添加
        //    foreach (var v in node.fixing_vars) 
        //    {
        //        best_feasible_solution.Add(v);
        //    }

        //    //TODO:回溯剪枝掉的var也得判断其值
            
        //    //未分支的变量，判断其是否为 1（为不失一般性，写的函数功能是判断是否为整数）
        //    foreach (var var_value in node.not_fixed_var_value_pairs) 
        //    {
        //        if (Convert.ToInt32(var_value.Value) > 0 && ISInteger(var_value.Value)) 
        //        {
        //            best_feasible_solution.Add(var_value.Key);
        //        }
        //    }
        //}

        #endregion

        #region 列生成

        /// <summary>在树节点"tree_node"章进行列生成
        /// 求得当前树节点的最优目标值
        /// </summary>
        /// <param name="tree_node"></param>
        public void CG(ref TreeNode tree_node)
        {
            //固定分支变量的值（==1）等于是另外加上变量的取值范围约束
            FixVars(tree_node.fixing_vars);            
            //迭代生成列
            for (; ; )
            {
                this.OBJVALUE = SolveRMP();
                obj_iter.Add(this.OBJVALUE);
                rcspT.Restart();
                int total_num_label = 0;
                //求解子问题，判断 检验数 < -1e-8 ?
                if (IsLPOpt())
                {
                    rcspT.Stop();
                    time_rcspp.Add(rcspT.Elapsed.TotalSeconds);
                    total_nums_rcspp += 1;                    
                    foreach (var node in Network.NodeSet)
                    {
                        total_num_label += node.LabelsForward.Count;
                    }
                    num_label_iter.Add(total_num_label);
                    break;
                }
                rcspT.Stop();
                time_rcspp.Add(rcspT.Elapsed.TotalSeconds);
                total_nums_rcspp+=1;
                total_num_label = 0;
                foreach (var node in Network.NodeSet)
                {
                    total_num_label+=node.LabelsForward.Count;
                }
                num_label_iter.Add(total_num_label);


                double col_coef = 0;
                int[] aj;
                foreach (Pairing column in R_C_SPP.New_Columns) 
                {
                    ColumnPool.Add(column);

                    col_coef = column.Coef;
                    aj = column.CoverMatrix;

                    INumVar column_var = masterModel.NumVar(0, 1, NumVarType.Float);
                    // function
                    Obj.Expr = masterModel.Sum(Obj.Expr, masterModel.Prod(col_coef, column_var));
                    // constrains
                    for (int i = 0; i < realistic_trip_num; i++)
                    {
                        Constraint[i].Expr = masterModel.Sum(Constraint[i].Expr,
                                                            masterModel.Prod(aj[i], column_var));
                    }

                    DvarSet.Add(column_var);
                    A_Matrix.Add(aj);
                    CoefSet.Add(col_coef);                    
                }                  
            }

            //传递信息给tree_node
            tree_node.obj_value = this.OBJVALUE;
            
            tree_node.not_fixed_var_value_pairs.Clear();
            for (int i = 0; i < DvarSet.Count; i++) 
            {                
                //将未分支的变量添加到待分支变量集合中
                //!被回溯“扔掉”的变量不能再添加到fixed_vars中,须加上 && tree_node.fixed_vars.Contains(i) == false
                if (tree_node.fixing_vars.Contains(i) == false && tree_node.fixed_vars.Contains(i) == false) 
                {
                    tree_node.not_fixed_var_value_pairs.Add(i, masterModel.GetValue(DvarSet[i]));
                }
            }

        }
        void FixVars(List<int> fixeing_vars) 
        {
            foreach (int i in fixeing_vars) 
            {
                DvarSet[i].LB = 1.0;
                DvarSet[i].UB = 1.0;
            }
        }
        /// <summary>返回当前RMP的目标函数值
        /// try catch:CPLEX求解linear relaxation 失败
        /// </summary>
        /// <returns></returns>
        public double SolveRMP()
        {            
            try
            {
                masterModel.Solve();                  
            }
            catch (ILOG.Concert.Exception ex)
            {
                Console.WriteLine("Current RMP can't solved, there might exit some error");
                Console.WriteLine("{0} from {1}", ex.Message, ex.Source);
            }

            return masterModel.GetObjValue();
        }
        
        bool IsLPOpt() 
        {
            Change_Arc_Length();
            this.R_C_SPP.ChooseCostDefinition(1);
            this.R_C_SPP.ShortestPath("Forward");
            //FindMultipleNewPColumn():True-找到新列，继续迭代;False-找不到新列，最优,停止
            this.R_C_SPP.FindMultipleNewPColumn(num_AddColumn);
            
            return R_C_SPP.Reduced_Cost > -1e-8;
        }
        void Change_Arc_Length()
        {
            int i, j;
            double price = 0;
            for (i = 0; i < realistic_trip_num; i++)
            {
                price = masterModel.GetDual(Constraint[i]);
                //Console.Write("dual price {0}: {1}\t", i + 1, price);
                for (j = 0; j < trip_num; j++)
                {
                    if (TripList[j].LineID == i + 1)
                    {
                        // constraint对应的dual 与tripList的不对应，由于删去了某些点，不能以Index来查找点ID
                        //triplist Nodeset几个引用间的关系也不明确，牵涉到拓扑、最短路、原始网络
                        TripList[j].Price = price;

                        //added 4-30
                        task_dualPrice[i + 1].Add(price);
                    }
                }
            }
        }
        #endregion

        //废弃
        //不用这个：引入了一个松弛变量和一个辅助变量
        //public void Build_RMP_auxiliary(InitialSolution IS)
        //{            
        //   // Network = IS.Net;
        //    //NodeSet = IS.NodeSet;            
        //    initialPath_num = IS.PathSet.Count;
        //    TripList = new List<Node>(); 

        //    int i, j;
        //    Node trip;
        //    for (i = 0; i < NodeSet.Count; i++)
        //    {
        //        trip = NodeSet[i];
        //        //trip.Visited = false;
        //        if (trip.ID != 0 && trip.ID != -1)
        //        {
        //            TripList.Add(trip);
        //        }
        //    }
        //    trip_num = TripList.Count;
        //    realistic_trip_num = NetWork.num_Physical_trip;//trip_num / CrewRules.MaxDays;

        //    DvarSet        = new List<INumVar>();//new ArrayList();
        //    CoefSet    = new List<double>();
        //    A_Matrix = new List<int[]>();
        //    PathSet  = new List<Pairing>();

        //    ExtraCovered = new ArrayList();
        //    Uncovered = new ArrayList();
        //    Penalty = new double[realistic_trip_num];
        //    U = new double[realistic_trip_num];

        //    //IS.PrepareInputForRMP(TripList); 
        //    for (i = 0; i < IS.Coefs.Count; i++) {
        //        CoefSet.Add(IS.Coefs[i]);
        //    }
        //    for (i = 0; i < IS.A_Matrix.Count; i++) {
        //        A_Matrix.Add(IS.A_Matrix[i]);
        //    }
        //    for (i = 0; i < IS.PathSet.Count; i++) {
        //        PathSet.Add(IS.PathSet[i]);
        //    }                                    

        //    masterModel = new Cplex();
        //    Obj = masterModel.AddMinimize();
        //    Constraint = new IRange[realistic_trip_num];
        //    /**按行建模**/

        //    //slack var
        //    int M = 2880 * CrewRules.MaxDays;
        //    for (i = 0; i < realistic_trip_num; i++)
        //    {
        //        Penalty[i] = 45 * M;//铭：取36--186总覆盖次数,34交路数
        //        U[i] = realistic_trip_num * M;
        //        INumVar b = masterModel.NumVar(0, 50, NumVarType.Float);
        //        ExtraCovered.Add(b);
        //        INumVar y = masterModel.NumVar(0, 1, NumVarType.Float);
        //        Uncovered.Add(y);
        //    }
        //    //vars and obj function
        //    for (j = 0; j < initialPath_num; j++) 
        //    {
        //        INumVar var = masterModel.NumVar(0, 1, NumVarType.Float);
        //        DvarSet.Add(var);
        //        Obj.Expr = masterModel.Sum(Obj.Expr, masterModel.Prod(CoefSet[j], (INumVar)DvarSet[j]));
        //    }
        //    //add slack vars to obj function
        //    for (i = 0; i < realistic_trip_num; i++) 
        //    {
        //        INumExpr expr1 = masterModel.NumExpr();
        //        expr1 = masterModel.Sum(expr1, masterModel.Prod(Penalty[i], (INumVar)ExtraCovered[i]));
        //        INumExpr expr2 = masterModel.NumExpr();
        //        expr2 = masterModel.Sum(expr2, masterModel.Prod(U[i], (INumVar)Uncovered[i]));

        //        Obj.Expr = masterModel.Sum(Obj.Expr, masterModel.Sum(expr1, expr2));
        //    }

        //    //constraints
        //    for (i = 0; i < realistic_trip_num; i++)
        //    {
        //        INumExpr expr = masterModel.NumExpr();
        //        //int num_trip_cover = 0;//该trip在第j条路中覆盖的次数（实际最短路求解时，得到的单条交路中不允许一个区段多天覆盖，这里这个步骤是保险起见，先写着）
        //        for (j = 0; j < initialPath_num; j++)
        //        {
        //            //for (k = 0; k < CrewRules.MaxDays; k++)
        //            //{
        //            //    num_trip_cover += A_Matrix[j][i + k * realistic_trip_num];
        //            //}
        //            expr = masterModel.Sum(expr, masterModel.Prod(A_Matrix[j][i], (INumVar)DvarSet[j]));
        //        }
        //        expr = masterModel.Sum(expr, masterModel.Prod(-1, (INumVar)ExtraCovered[i]), (INumVar)Uncovered[i]);
        //        //Constraint[i] = masterModel.AddGe(expr, 1);
        //        Constraint[i] = masterModel.AddEq(expr, 1);
        //    }
        //}

        //同废弃，但这二者没有必然调用关系
        //public void LinearRelaxation() 
        //{            

        //    Topological2 topo;
        //    R_C_SPP = new RCSPP(Network, out topo);
        //    //RCSPP R_C_SPP = new RCSPP();              
        //    //R_C_SPP.UnProcessed = new List<Node>();
        //    //for (i = 0; i < Topo.Order.Count; i++)
        //    //{
        //    //    R_C_SPP.UnProcessed.Add(Topo.Order[i]);
        //    //}

        //    int iter_num = 0;

        //    masterModel.SetParam(Cplex.IntParam.RootAlg, Cplex.Algorithm.Primal);         
        //    for (; ; ) 
        //    {

        //        if (masterModel.Solve())
        //        {
        //            //output solution information
        //            Console.WriteLine("{0},{1}", "masterProblem ObjValue: ", masterModel.GetObjValue());
        //            Console.WriteLine("iteration time: " + iter_num++);

        //            #region stuck test, restrict iter time
        //            //if (iter_num > 200)
        //            //{
        //            //    int p = 0;
        //            //    for (p = PathSet.Count - 1; p >= PathSet.Count - 5; p--)
        //            //    {
        //            //        //if (masterModel.GetReducedCost((INumVar)X[p]) != 0)
        //            //        //{
        //            //            Console.Write("X" + p + ": " + masterModel.GetReducedCost((INumVar)X[p]) + "\t");
        //            //        //}
        //            //    }
        //            //    Console.WriteLine("\n//////");
        //            //    Node trip;
        //            //    int id;
        //            //    for (p = 0; p < PathSet.Last().Arcs.Count; p++) {
        //            //        Console.WriteLine("ARC COST: " + PathSet.Last().Arcs[p].Cost + ", ");
        //            //    }

        //            //    for ( p = 0; p < PathSet.Last().Arcs.Count - 1; p++)
        //            //    {
        //            //        trip = PathSet.Last().Arcs[p].D_Point;
        //            //        id = trip.LineID;

        //            //        Console.Write(id + " dual: " + masterModel.GetDual(Constraint[id - 1]) + ",\t");
        //            //    }
        //            //    //break;
        //            //}
        //            #endregion

        //            Change_Arc_Length();                    

        //            R_C_SPP.ShortestPath("Forward");//必须保证unprocessed里的点未处理，对Labels清空恢复初始状态
        //            R_C_SPP.FindNewPath();
        //            Console.WriteLine("\n子问题 objValue: " + R_C_SPP.Reduced_Cost);                    

        //            if (R_C_SPP.Reduced_Cost >= -0.1)
        //            {
        //                break;
        //            }
        //            //else if (Check_Stuck(R_C_SPP.Reduced_Cost, R_C_SPP.New_Path)) 
        //            //{
        //            //    int yy = 0;
        //            //    break;
        //            //}

        //            //Add Column          
        //            ColumnPool.Add(R_C_SPP.New_Column);

        //            Pairing newPath  = R_C_SPP.New_Column;
        //            double newCoef = newPath.Coef;
        //            int[] newAji = newPath.CoverMatrix;
        //            INumVar newColumn = masterModel.NumVar(0, 1, NumVarType.Float);

        //            Obj.Expr = masterModel.Sum(Obj.Expr, masterModel.Prod(newCoef, newColumn));
        //            for (int i = 0; i < realistic_trip_num; i++)
        //            {                        
        //                Constraint[i].Expr = masterModel.Sum(Constraint[i].Expr,
        //                                                    masterModel.Prod(newAji[i], newColumn));
        //            }
        //            DvarSet.Add(newColumn);
        //            A_Matrix.Add(newAji);
        //            CoefSet.Add(newCoef);
        //            PathSet.Add(newPath);
        //        }
        //        else 
        //        {
        //            Console.WriteLine("failure to find solution of master problem!!! ");
        //            break;
        //        }
        //    }
        //}

        //暂时不用
        //void Add_Columns(int add_type)
        //{
        //    switch (add_type)
        //    {
        //        case 0:

        //            break;
        //        case 1:
        //            break;
        //        default:
        //            Console.WriteLine("wrong input type");
        //            Thread.Sleep(3000);
        //            break;
        //    }
        //}
         
    }

}
