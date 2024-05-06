using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Administration
{
    /// <summary>
    /// 流程线程管理类
    /// </summary>
    public abstract class Thread_Auto_Base
    {
        /// <summary>
        /// 流程阻塞句柄
        /// </summary>
        public abstract ManualResetEvent Interrupt { get; set; }

        /// <summary>
        /// 流程日志事件
        /// </summary>
        public abstract event Action<DateTime, string> Run_LogEvent;

        /// <summary>
        /// 流程线程管理创建事件
        /// </summary>
        public static event Action<DateTime> NewClass_Run;

        /// <summary>
        /// 流程派生集合
        /// </summary>
        public static System.Collections.Generic.List<ProductionThreadBase> Auto_Th { get; private set; } = new System.Collections.Generic.List<ProductionThreadBase>();

        public Thread_Auto_Base()
        {

        }

        /// <summary>
        /// 流程初始化
        /// </summary>
        /// <param name="spintime">中断时间（ms）</param>
        public static void NewClass(int spintime = 50)
        {
            Type baseType = typeof(Thread_Auto_Base);
            Assembly assembly = Assembly.GetEntryAssembly();
            Type[] derivedTypes = assembly.GetTypes().Where(t => t.IsSubclassOf(baseType)).ToArray();
            foreach (Type derivedType in derivedTypes)
            {
                object instance = Activator.CreateInstance(derivedType);
                MethodInfo[] methods = derivedType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                foreach (MethodInfo method in methods)
                {
                    var t = method.GetCustomAttributes(typeof(ProductionThreadBase), inherit: true);
                    if (t.Length > 0)
                        Thread_Configuration(derivedType.Name, method, instance, spintime);
                }
            }
            NewClass_Run?.Invoke(DateTime.Now);
        }

        private static void Thread_Configuration(string class_na, MethodInfo method, object class_new, int spintime)
        {
            Thread_Auto_Base instance = class_new as Thread_Auto_Base ?? throw new Exception("Automatic thread conversion exception");
            if (instance.Interrupt == null)
                instance.Interrupt = new ManualResetEvent(true);
            DescriptionAttribute descriptionAttribute = (DescriptionAttribute)method.GetCustomAttribute(typeof(DescriptionAttribute));
            ProductionThreadBase threadBase = new ProductionThreadBase()
            {
                Target = method.Name,
                Thread_Name = descriptionAttribute?.Description ?? method.Name,
                Is_Running = true,
                New_Thread = new Thread(() =>
                {
                    while (true)
                    {
                        try
                        {
                            instance.Interrupt?.WaitOne();
                            Thread.Sleep(spintime);
                            method.Invoke(instance, new Thread_Auto_Base[] { instance });
                        }
                        catch (ThreadAbortException ex)
                        {
                            instance.ThreadRestartEvent(class_na, instance, ex);
                            Thread_Configuration(class_na, method, class_new, spintime);
                            return;
                        }
                        catch (TargetInvocationException ex)
                        {
                            Exception innerException = ex.InnerException;
                            instance.ThreadError(class_na, instance, innerException);
                            Thread_Configuration(class_na, method, class_new, spintime);
                            return;
                        }
                    }
                }),
            };
            threadBase.New_Thread.Name = class_na + "." + threadBase.Thread_Name;
            threadBase.New_Thread.IsBackground = true;
            threadBase.New_Thread.Start();
            Auto_Th.Add(threadBase);
        }

        /// <summary>
        /// 流程取消
        /// </summary>
        public static void Thraead_Dispose()
        {
            if (Auto_Th != null)
                if (Auto_Th.Count > 0)
                {
                    var t = Thread_Auto_Base.Auto_Th.ToList();
                    foreach (var item in t)
                    {
                        item.Is_Running = false;
                        Thread_Auto_Base.Auto_Th.Remove(item);
                        item.New_Thread.Abort();
                        item.New_Thread.Join();
                    }
                    GC.Collect();
                }
        }

        /// <summary>
        /// 流程取消
        /// </summary>
        /// <param name="Thread_Name">流程名称</param>
        public static void Thraead_Dispose(string Thread_Name)
        {
            if (Auto_Th != null)
                if (Auto_Th.Count > 0)
                {
                    var t = Thread_Auto_Base.Auto_Th.FirstOrDefault(x => x.Thread_Name == Thread_Name);
                    t.Is_Running = false;
                    Thread_Auto_Base.Auto_Th.Remove(t);
                    t.New_Thread.Abort();
                    t.New_Thread.Join();
                    GC.Collect();
                }
        }

        /// <summary>
        /// 初始化流程
        /// </summary>
        /// <param name="thread"></param>
        public abstract void Initialize(object thread);

        /// <summary>
        /// 主流程
        /// </summary>
        /// <param name="thread">派生对象</param>
        [ProductionThreadBase]
        public abstract void Main(Thread_Auto_Base thread);

        /// <summary>
        /// 主流程取消通知
        /// </summary>
        /// <param name="class_na">类名称</param>
        /// <param name="thread">流程对象</param>
        /// <param name="ex">取消异常</param>
        public abstract void ThreadRestartEvent(string class_na, Thread_Auto_Base thread, ThreadAbortException ex);

        /// <summary>
        /// 主流程异常通知
        /// </summary>
        /// <param name="class_na">类名称</param>
        /// <param name="thread">流程对象</param>
        /// <param name="exception">异常</param>
        public abstract void ThreadError(string class_na, Thread_Auto_Base thread, Exception exception);
    }
}
