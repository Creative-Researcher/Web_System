using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Web_System.Models;
using Dapper;


namespace Web_System.Controllers
{
    public class HomeController : Controller
    {
        private string connString = "Server=localhost;Database=pirds;Uid=root;Pwd=1234;";

        public IActionResult Login() => View();

        [HttpPost]
        public IActionResult ProcessLogin(string username, string password)
        {
            User loggedInUser = null;
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();
                    string sql = "SELECT Id, Role, FullName, POSITION FROM Users WHERE Username=@u AND Password=@p";
                    using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@u", username);
                        cmd.Parameters.AddWithValue("@p", password);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                loggedInUser = new User
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    Role = reader["Role"].ToString().Trim(),
                                    FullName = reader["FullName"].ToString(),
                                    Position = reader["POSITION"].ToString()
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Database Error: " + ex.Message;
                return View("Login");
            }

            if (loggedInUser != null)
            {
                if (string.Equals(loggedInUser.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    return RedirectToAction("AdminDashboard");
                }
                return RedirectToAction("UserDashboard", new { id = loggedInUser.Id });
            }

            ViewBag.ErrorMessage = "Invalid Credentials!";
            return View("Login");
        }

        public IActionResult AdminDashboard()
        {
            List<User> users = new List<User>();
            var predictiveReports = new List<dynamic>();
            var usageHistory = new List<dynamic>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();

                    // 1. Fetch Users List
                    string userSql = "SELECT Id, FullName, POSITION, Role FROM Users";
                    using (MySqlCommand cmd = new MySqlCommand(userSql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            users.Add(new User
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                FullName = reader["FullName"].ToString(),
                                Position = reader["POSITION"].ToString(),
                                Role = reader["Role"].ToString()
                            });
                        }
                    }

                    // 2. Fetch Planning Inputs (Problems & AI Solutions)
                    string reqSql = @"SELECT p.*, u.FullName 
                                     FROM planninginput p 
                                     LEFT JOIN Users u ON p.faculty_id = u.Id 
                                     ORDER BY p.planning_id DESC";

                    // 1. Create the connection string (Make sure your DB name and password are correct)
                    string connString = "Server=localhost;Database=pirds;Uid=root;Pwd=1234;";

                    // 2. Open the connection - this "unlocks" the variable 'connection'
                    using (var connection = new MySqlConnection(connString))
                    {
                        // 3. Fetch Predictive Reports
                        string queryReports = @"SELECT p.planning_id AS Id,
                               p.resource_request AS ProblemDetails,
                               p.status AS Prediction, 
                               p.ai_suggestion AS AISuggestion,
                               u.FullName AS Username, 
                               p.planning_period AS Period
                        FROM planninginput p 
                        INNER JOIN Users u ON p.faculty_id = u.Id 
                        ORDER BY p.planning_id DESC";

                        // Now 'connection' will no longer be red!
                        var reports = connection.Query<dynamic>(queryReports).ToList();
                        ViewBag.PredictiveReports = reports;

                        // 4. Fetch Historical Trends
                        string queryHistory = "SELECT usage_date AS Date, description AS Description, quantity_used AS Quantity FROM historicalusage";
                        var history = connection.Query<dynamic>(queryHistory).ToList();
                        ViewBag.History = history;

                        // 5. Fetch Data for Users (The Model) - notice we use 'connection' here too!
                        var userList = connection.Query<Web_System.Models.User>("SELECT * FROM users WHERE Role != 'Admin'").ToList();

                        return View(userList);

                        // Fetch the Historical Trends data
                        string queryhistory = "SELECT usage_date AS Date, description AS ResourceLog, quantity_used AS Utilization FROM historicalusage ORDER BY history_date DESC";

                        // Convert the database results into a list
                        var historyData = connection.Query<dynamic>(queryHistory).ToList();

                        // Send it to the View
                        ViewBag.History = historyData;

                        return View(userList);
                        // 1. Get the counts for the Chart
                        ViewBag.GeneralCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM planninginput WHERE status LIKE '%General%'");
                        ViewBag.EconomyCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM planninginput WHERE status LIKE '%Economic%'");
                        ViewBag.IndustryCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM planninginput WHERE status LIKE '%Industrial%'");
                        ViewBag.BusinessCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM planninginput WHERE status LIKE '%Business%'");

                        // 2. Determine the "Most Needed" for the text box
                        var counts = new Dictionary<string, int> {
                                    { "General", (int)ViewBag.GeneralCount },
                                    { "Economic", (int)ViewBag.EconomyCount },
                                    { "Industrial", (int)ViewBag.IndustryCount },
                                    { "Business", (int)ViewBag.BusinessCount }
                                     };
                        ViewBag.MostNeeded = counts.OrderByDescending(x => x.Value).First().Key;
                    }


                    using (MySqlCommand reqCmd = new MySqlCommand(reqSql, conn))
                    using (var reader = reqCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            dynamic report = new ExpandoObject();
                            report.planning_id = reader["planning_id"];
                            report.StaffName = reader["FullName"]?.ToString() ?? "System/Guest";
                            report.ProblemDetails = reader["resource_request"]?.ToString() ?? "No details provided";
                            report.SystemPrediction = reader["status"]?.ToString() ?? "General Need";
                            report.AISuggestion = reader["ai_suggestion"]?.ToString() ?? "Analysis Pending";
                            report.Period = reader["planning_period"]?.ToString() ?? "N/A";
                            predictiveReports.Add(report);
                        }
                    }

                    // 3. Historical Data
                    string historySql = "SELECT usage_date, description, quantity_used FROM historicalusage";

                    using (MySqlCommand hCmd = new MySqlCommand(historySql, conn))
                    using (var hReader = hCmd.ExecuteReader())
                    {
                        while (hReader.Read())
                        {
                            dynamic entry = new ExpandoObject();

                            // 2. Use the correct column names inside hReader[...]
                            // We assign them to the property names your View expects (Date, Description, Quantity)
                            entry.Date = hReader["usage_date"].ToString();
                            entry.Description = hReader["description"].ToString();
                            entry.Quantity = hReader["quantity_used"].ToString();

                            usageHistory.Add(entry);
                        }
                    }
                }

                ViewBag.PredictiveReports = predictiveReports;
                ViewBag.History = usageHistory;

                // CHART LOGIC: Counts occurrences of specific Predictive Solutions
                ViewBag.EconomyCount = predictiveReports.Count(r => ((string)r.SystemPrediction).Contains("Economic"));
                ViewBag.IndustryCount = predictiveReports.Count(r => ((string)r.SystemPrediction).Contains("Industrial"));
                ViewBag.BusinessCount = predictiveReports.Count(r => ((string)r.SystemPrediction).Contains("Business"));
                ViewBag.GeneralCount = predictiveReports.Count(r => ((string)r.SystemPrediction).Contains("General"));

                ViewBag.MostNeeded = predictiveReports.Any()
                    ? predictiveReports.GroupBy(r => (string)r.SystemPrediction)
                                       .OrderByDescending(g => g.Count())
                                       .First().Key
                    : "System Idle";
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Admin Dashboard Error: " + ex.Message;
            }

            return View("AdminDashboard", users);
        }

        public IActionResult UserDashboard(int? id)
        {
            if (id == null) return RedirectToAction("Login");

            User userProfile = null;
            var myRequests = new List<dynamic>();

            using (MySqlConnection conn = new MySqlConnection(connString))
            {
                conn.Open();
                string userSql = "SELECT * FROM Users WHERE Id = @id";
                using (MySqlCommand uCmd = new MySqlCommand(userSql, conn))
                {
                    uCmd.Parameters.AddWithValue("@id", id);
                    using (var reader = uCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            userProfile = new User
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                FullName = reader["FullName"].ToString(),
                                Position = reader["POSITION"].ToString(),
                                Role = reader["Role"].ToString()
                            };
                        }
                    }
                }

                if (userProfile == null) return RedirectToAction("Login");

                string sql = "SELECT * FROM planninginput WHERE faculty_id = @id ORDER BY planning_id DESC";
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            dynamic req = new ExpandoObject();
                            req.Details = reader["resource_request"].ToString();
                            req.Status = reader["status"].ToString();
                            req.Period = reader["planning_period"].ToString();
                            myRequests.Add(req);
                        }
                    }
                }
            }

            ViewBag.MyRequests = myRequests;
            return View("User", userProfile);
        }

        [HttpPost]
        public IActionResult SubmitPlanningData(int facultyId, string resourceRequest, string planningPeriod)
        {
            // Initializing system response
            string prediction = "General Need";
            string suggestion = "Standard resource allocation and administrative review recommended.";

            string reqLower = resourceRequest.ToLower();

            // PREDICTIVE AI ENGINE: Maps Institutional Problems to Solutions
            if (reqLower.Contains("market") || reqLower.Contains("economy") || reqLower.Contains("finance") || reqLower.Contains("price"))
            {
                prediction = "Economic Stability Solution";
                suggestion = "Strategic Action: Allocate fiscal buffers and adjust budgets for market volatility.";
            }
            else if (reqLower.Contains("industrial") || reqLower.Contains("machinery") || reqLower.Contains("broken") || reqLower.Contains("repair"))
            {
                prediction = "Industrial Operational Recovery";
                suggestion = "Strategic Action: Immediate procurement of replacement parts and maintenance scheduling.";
            }
            else if (reqLower.Contains("business") || reqLower.Contains("marketing") || reqLower.Contains("revenue") || reqLower.Contains("growth"))
            {
                prediction = "Business Development Strategy";
                suggestion = "Strategic Action: Expand outreach programs and invest in brand positioning.";
            }

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();
                    // Saving the Problem (resource_request), Prediction (status), and Advice (ai_suggestion)
                    string sql = "INSERT INTO planninginput (faculty_id, resource_request, planning_period, status, ai_suggestion) " +
                                 "VALUES (@fid, @req, @period, @status, @sugg)";
                    using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@fid", facultyId);
                        cmd.Parameters.AddWithValue("@req", resourceRequest);
                        cmd.Parameters.AddWithValue("@period", planningPeriod);
                        cmd.Parameters.AddWithValue("@status", prediction);
                        cmd.Parameters.AddWithValue("@sugg", suggestion);
                        cmd.ExecuteNonQuery();
                    }
                }
                TempData["SuccessMessage"] = "Institutional data analyzed and recorded successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "System Error during analysis: " + ex.Message;
            }

            return RedirectToAction("UserDashboard", new { id = facultyId });
        }

        public IActionResult ContactAdmin()
        {
            ViewBag.AdminEmail = "gandalusaoryza9@gmail.com";
            ViewBag.AdminPhone = "+63 981 711 4634";
            ViewBag.OfficeHours = "Monday - Friday, 8:00 AM - 5:00 PM";
            return View();
        }
        [HttpPost]
        public IActionResult ArchiveProblem(int id)
        {
            using (var connection = new MySqlConnection(connString))
            {
                // 1. Copy the data to History
                string archiveSql = @"INSERT INTO historicalusage (usage_date, description, quantity_used)
                              SELECT CURRENT_DATE, resource_request, 100 
                              FROM planninginput WHERE planning_id = @id";

                connection.Execute(archiveSql, new { id = id });

                // 2. Delete it from Active Problems (so the dashboard stays clean)
                connection.Execute("DELETE FROM planninginput WHERE planning_id = @id", new { id = id });

                TempData["SuccessMessage"] = "Problem archived to Historical Trends successfully!";
                return RedirectToAction("AdminDashboard");
            }
        }

        public IActionResult GenerateForecast() => View();

        public IActionResult Logout() => RedirectToAction("Login");
    }
}