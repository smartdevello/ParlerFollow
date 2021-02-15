using OpenQA.Selenium;
using System;
using System.Windows.Forms;
using System.Configuration;
using System.IO;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Diagnostics;
using OpenQA.Selenium.Chrome;
using System.Linq;
using System.Threading;
using Timer = System.Timers.Timer;
using OpenQA.Selenium.Support.UI;
using System.Timers;

namespace ParlerFollow
{
    public partial class Form1 : Form
    {

        IWebDriver driver;
        Configuration config;
        Timer displayTimer;
        Stopwatch _timer = new Stopwatch();
        string chromeVersion;
        string appPath = Directory.GetCurrentDirectory();
        string keyword;
        decimal perDay;
        int remainTime = 0, NumberofRun, NumberoffollowPeople;

        Thread follower;
        ManualResetEvent _stopEvent = new ManualResetEvent(false);
        ManualResetEvent _pauseEvent = new ManualResetEvent(false);

        bool isRunning = false, restricted = false, KeyRegistered = false;
        public delegate void OutputDelegate(string element, string value);

        public Form1()
        {
            InitializeComponent();
            config = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath);

        }
        private void Form1_Load(object sender, EventArgs e)
        {

            RegistryKey rkey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\PalerSettings");

            if (rkey!= null)
            {
                var keyValue = rkey.GetValue("NumberofRun");
                if (keyValue !=null)
                {
                    NumberofRun = (int)keyValue;
                } else
                {
                    keyValue = 0;
                    rkey.SetValue("NumberofRun", 0);
                }

                keyValue = rkey.GetValue("NumberoffollowPeople");
                if (keyValue != null)
                {
                    NumberoffollowPeople = (int)keyValue;
                }
                else
                {
                    NumberoffollowPeople = 0;
                    rkey.SetValue("NumberoffollowPeople", 0);
                }

                keyValue = rkey.GetValue("KeyRegistered");
                if (keyValue != null)
                {
                    KeyRegistered = Convert.ToBoolean(keyValue);
                }

            }

            if (NumberoffollowPeople > 500 || NumberofRun > 50)
            {
                restricted = true;
            }
            NumberofRun++;

            foreach (Browser browser in GetBrowsers())
            {
                if (browser.Name.Contains("Chrome"))
                {
                    chromeVersion = browser.Version;
                    break;
                }
            }
            btn_Start.Enabled = false;
            btn_Pause.Enabled = false;
            btn_Stop.Enabled = false;


            try
            {
                txt_Keyword.Text = ReadSetting("follow_Keyword");
                if (txt_Keyword.Text == "") txt_Keyword.Text = "nurse";
                txt_Perday.Value = Convert.ToDecimal(ReadSetting("follow_Perday"));
                if (txt_Perday.Value == 1) txt_Perday.Value = 400;
            }
            catch (Exception ex)
            {
                txt_Keyword.Text = "nurse";
                txt_Perday.Value = 400;
            }


        }
        private void btn_Login_Click(object sender, EventArgs e)
        {
            ChromeDriverService driverService = null;
            if (chromeVersion.Contains("86."))
            {
                driverService = ChromeDriverService.CreateDefaultService(appPath + "\\chrome86\\");
            }
            else if (chromeVersion.Contains("87."))
            {
                driverService = ChromeDriverService.CreateDefaultService(appPath + "\\chrome87\\");
            }
            else
            {
                MessageBox.Show("Please Update your Chrome version to 86.x or 87.x");
                return;
            }

            if (string.IsNullOrEmpty(txt_Useremail.Text) || string.IsNullOrEmpty(txt_Password.Text))
            {
                MessageBox.Show("Please Input the Username and password.");
                return;
            }
            driverService.HideCommandPromptWindow = true;
            ChromeOptions chromeOptions = new ChromeOptions();

            //chromeOptions.AddArgument("user-data-dir=C:\\Users\\micha\\AppData\\Local\\Google\\Chrome\\User Data");
            //chromeOptions.AddArgument("profile-directory=Profile 1");

            try
            {
                driver = new ChromeDriver(driverService, chromeOptions);
            }
            catch (WebDriverException ex)
            {
                try
                {
                    var chromeDriverProcesses = Process.GetProcesses().Where(pr => pr.ProcessName.Contains("chrome"));
                    foreach (var process in chromeDriverProcesses)
                    {
                        process.Kill();
                    }
                    driver = new ChromeDriver(driverService, chromeOptions);
                }
                catch(Exception lastex)
                {
                    MessageBox.Show("Please End All running Chrome instances and try again!");
                    if (driver !=null) driver.Quit();
                    return;
                }

            }

            try
            {
                driver.Navigate().GoToUrl("https://parler.com/auth/access");

                driver.FindElement(By.Id("wc--2--login")).Click();
                driver.FindElement(By.Id("mat-input-0")).SendKeys(txt_Useremail.Text);
                driver.FindElement(By.Id("mat-input-1")).SendKeys(txt_Password.Text);

                var div = driver.FindElement(By.Id("auth-form--actions"));
                var btns = div.FindElements(By.TagName("button"));
                btns[0].Click();

            }
            catch (Exception ex)
            {

            }

            while (true)
            {
                if (driver != null)
                {
                    try
                    {
                        if (driver.Url != null && driver.Url.Contains("parler.com/feed"))
                        {
                            btn_Login.Enabled = false;
                            btn_Start.Enabled = true;
                            break;
                        }
                        else
                        {
                            Thread.Sleep(1000);
                        }
                    }
                    catch (Exception ex)
                    {
                        break;
                    }

                }
            }

        }
        private void btn_Savesetting_Click(object sender, EventArgs e)
        {
            AddUpdateAppSettings("follow_Keyword", txt_Keyword.Text);
            AddUpdateAppSettings("follow_Perday", txt_Perday.Value.ToString());
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (follower != null && follower.IsAlive)
            {
                follower.Join();
            }
            if (driver != null)
            {
                driver.Quit();
            }
            RegistryKey rkey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\PalerSettings");
            rkey.SetValue("NumberoffollowPeople", NumberoffollowPeople);
            rkey.SetValue("NumberofRun", NumberofRun);

        }

        private void btn_Start_Click(object sender, EventArgs e)
        {
            if (!KeyRegistered)            
            {
                var m2 = new  Form2();
                m2.Show();
            }
            if (restricted) return;
            if (driver == null || driver.Url == "") return;
            if (driver.WindowHandles.Count > 1) return;

            if (string.IsNullOrEmpty(txt_Keyword.Text) || txt_Perday.Value == 0)
            {
                MessageBox.Show("Please Fill out the Setting values");
                return;
            }

            lblcurrentIndex.Text = "0";
            keyword = txt_Keyword.Text;
            perDay = txt_Perday.Value;

            follower = new Thread(Follow);
            btn_Login.Enabled = false;
            btn_Start.Enabled = false;
            btn_Pause.Enabled = true;
            btn_Stop.Enabled = true;

            _pauseEvent.Reset();
            _stopEvent.Reset();
            isRunning = true;

            follower.Start();
            

        }

        private void DisplayRemainingTime(string element, string value)
        {
            switch (element)
            {
                case "lblcurrentIndex":
                    lblcurrentIndex.Text = value;
                    break;
                case "lblremainingTime":
                    lblremainingTime.Text = value;
                    break;
                default:
                    break;
            }

        }
        private void Follow()
        {
            
            try
            {
                if (!driver.Url.Contains("https://parler.com/search"))
                {
                    driver.Navigate().GoToUrl("https://parler.com/search?searchTerm=" + keyword);

                } else
                {
                    driver.Navigate().GoToUrl("https://parler.com/search?searchTerm=" + keyword);
                }

                var wait = new WebDriverWait(driver, TimeSpan.FromMinutes(1));
                wait.Until(ExpectedConditions.ElementIsVisible(By.CssSelector("div#search-results--users")));

                bool exitflag = false;
                var body = driver.FindElement(By.TagName("body"));
                var peoples = driver.FindElements(By.CssSelector("button[id^=\"action-button--width--follow\"]"));
                var peopleName = driver.FindElements(By.CssSelector("div#user-card--identity-row a.name"));

                var js = ((IJavaScriptExecutor)driver);
                var searchInput = driver.FindElement(By.CssSelector("input#search-input"));

                bool checkLastone = true;

                displayTimer = new Timer(1000);
                displayTimer.Elapsed += displayTimer_callback;
                displayTimer.AutoReset = true;
                displayTimer.Enabled = true;


                _pauseEvent.Set();
                for (int i = 0; i< peoples.Count; i++)
                {

                    _pauseEvent.WaitOne(Timeout.Infinite);
                    if (_stopEvent.WaitOne(0)) break; 

                    try
                    {
                        if (checkLastone)
                        {
                            int j = peoples.Count - 1;
                            string temp = peoples[j].Text;
                            if (temp == "Following")
                            {
                                i = peoples.Count - 1;
                                js.ExecuteScript("arguments[0].scrollIntoView(true);", peoples[i]);
                                Thread.Sleep(5000);
                            }
                            checkLastone = false;
                        }
                        if (i == peoples.Count - 1)
                        {
                            peoples = driver.FindElements(By.CssSelector("button[id^=\"action-button--width--follow\"]"));
                            peopleName = driver.FindElements(By.CssSelector("div#user-card--identity-row a.name"));
                            js.ExecuteScript("arguments[0].scrollIntoView(true);", peoples[i]);
                            checkLastone = true;
                        }


                        var outputDelegate = new OutputDelegate(DisplayRemainingTime);

                        string disName = string.Format("{0} , {1}th people", peopleName[i].Text, i+1);
                        this.Invoke(outputDelegate, "lblcurrentIndex", disName);

                        string txt = peoples[i].Text;
                        var res = js.ExecuteScript("arguments[0].scrollIntoView(true);", peoples[i]);
                        if (txt == "Following") continue;
                        res = js.ExecuteScript("arguments[0].click();", peoples[i]);
                        NumberoffollowPeople++;

                        _pauseEvent.Reset();
                        _timer.Reset();
                        _timer.Restart();
                        remainTime = Convert.ToInt32(3600 * 24 / perDay);

                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.Message.ToString());
                    }
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message.ToString());
            }
        }
        private void btn_Pause_Click(object sender, EventArgs e)
        {
            if (isRunning)
            {
                _timer.Stop();
                isRunning = false;
                btn_Pause.Text = "Resume";

            }
            else
            {
                isRunning = true;
                btn_Pause.Text = "Pause";
                _timer.Start();
            }
        }

        private void displayTimer_callback(Object source, ElapsedEventArgs e)
        {
            try
            {
                var outputDelegate = new OutputDelegate(DisplayRemainingTime);
                int currentTime = remainTime - Convert.ToInt32(_timer.ElapsedMilliseconds / 1000);

                this.Invoke(outputDelegate, "lblremainingTime", currentTime.ToString());

                if (_stopEvent.WaitOne(0) || currentTime <= 0)
                {
                        this.Invoke(outputDelegate, "lblremainingTime", "0");
                        _pauseEvent.Set();
                        return;
                }
            }catch(Exception ex)
            {

            }


        }
        public List<Browser> GetBrowsers()
        {
            List<Browser> browsers = new List<Browser>();
            RegistryKey browserKeys;
            //on 64bit the browsers are in a different location
            browserKeys = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Clients\StartMenuInternet");
            if (browserKeys == null)
                browserKeys = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Clients\StartMenuInternet");
            string[] browserNames = browserKeys.GetSubKeyNames();

            for (int i = 0; i < browserNames.Length; i++)
            {
                Browser browser = new Browser();
                RegistryKey browserKey = browserKeys.OpenSubKey(browserNames[i]);
                browser.Name = (string)browserKey.GetValue(null);
                RegistryKey browserKeyPath = browserKey.OpenSubKey(@"shell\open\command");
                browser.Path = (string)browserKeyPath.GetValue(null).ToString().Trim();
                RegistryKey browserIconPath = browserKey.OpenSubKey(@"DefaultIcon");
                browser.IconPath = (string)browserIconPath.GetValue(null).ToString().Trim();
                browsers.Add(browser);
                if (browser.Path != null)
                    browser.Version = FileVersionInfo.GetVersionInfo(browser.Path.Replace("\"", "")).FileVersion;
                else
                    browser.Version = "unknown";
            }
            return browsers;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (follower != null && follower.IsAlive)
            {
                follower.Join();
            }
            if (driver != null)
            {
                driver.Quit();
            }
            RegistryKey rkey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\PalerSettings");
            rkey.SetValue("NumberoffollowPeople", NumberoffollowPeople);
            rkey.SetValue("NumberofRun", NumberofRun);
            this.Close();

        }

        public string ReadSetting(string key)
        {
            string result = "";
            try
            {
                var appSettings = ConfigurationManager.AppSettings;
                result = appSettings[key] ?? "0";

            }
            catch (ConfigurationErrorsException)
            {

            }
            return result;
        }
        public void AddUpdateAppSettings(string key, string value)
        {
            try
            {
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var settings = configFile.AppSettings.Settings;
                if (settings[key] == null)
                {
                    settings.Add(key, value);
                }
                else
                {
                    settings[key].Value = value;
                }
                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
            }
            catch (ConfigurationErrorsException)
            {

            }
        }



        private void btn_Stop_Click(object sender, EventArgs e)
        {
            var outputDelegate = new OutputDelegate(DisplayRemainingTime);
            this.Invoke(outputDelegate, "lblremainingTime", "0");

            isRunning = false;
            _stopEvent.Set();
            _pauseEvent.Set();


            btn_Start.Enabled = true;
            btn_Pause.Enabled = false;
            btn_Stop.Enabled = false;
            follower.Join();

        }
    }
}
public class Browser
{
    public string Name { get; set; }
    public string Path { get; set; }
    public string IconPath { get; set; }
    public string Version { get; set; }
}
