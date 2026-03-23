using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection.PortableExecutable;
using Web_System.Models;

namespace Web_System.Controllers
{
    public class HomeController : Controller
    {
        private string connString = "Server=localhost;Database=pirds;Uid=root;Pwd=1234;";

        public IActionResult Login() => View();

        [HttpPost]
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
                                // 1. Map the database values to your User object
                                loggedInUser = new User
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    Role = reader["Role"].ToString().Trim(),
                                    FullName = reader["FullName"].ToString(),
                                    Position = reader["POSITION"].ToString()
                                };

                                // 2. Debugging: This will show in your Visual Studio Output window
                                Console.WriteLine($"LOGIN SUCCESS: User={loggedInUser.FullName}, Role='{loggedInUser.Role}'");
                            }
                            else
                            {
                                // 3. Debugging: Query returned 0 rows
                                Console.WriteLine("LOGIN FAIL: No user found with those credentials.");
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

            // 4. Decision Logic: Only runs if the database connection finished successfully
            if (loggedInUser != null)
            {
                // Check if the trimmed role is exactly "Admin"
                if (string.Equals(loggedInUser.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    return RedirectToAction("AdminDashboard");
                }

                return RedirectToAction("UserDashboard", new { id = loggedInUser.Id });
            }

            // 5. Fallback: If no user was found, return to login with error
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

                    // 1. Fetch Users
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

                    // 2. Fetch Predictive Reports
                    string reqSql = "SELECT p.*, u.FullName FROM PlanningInput p JOIN Users u ON p.faculty_id = u.Id ORDER BY p.planning_id DESC";
                    using (MySqlCommand reqCmd = new MySqlCommand(reqSql, conn))
                    using (var reader = reqCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            dynamic report = new ExpandoObject();
                            // Use the null-coalescing operator for safety
                            report.StaffName = reader["FullName"]?.ToString() ?? "Unknown Staff";
                            report.ProblemDetails = reader["resource_request"]?.ToString() ?? "No details provided";
                            report.SystemPrediction = reader["status"]?.ToString() ?? "General Institutional Need";
                            report.AISuggestion = reader["ai_suggestion"]?.ToString() ?? "Standard Review Required";
                            report.Period = reader["planning_period"]?.ToString() ?? "N/A";
                            predictiveReports.Add(report);
                        }
                    }

                    // 3. Fetch Usage History
                    string historySql = "SELECT `Date`, `Item Description`, `Quantity` FROM historicalusage";
                    using (MySqlCommand historyCmd = new MySqlCommand(historySql, conn))
                    using (var reader = historyCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            dynamic entry = new ExpandoObject();
                            entry.Date = reader.IsDBNull(0) ? "N/A" : reader.GetDateTime(0).ToString("yyyy-MM-dd");
                            entry.Description = reader["Item Description"].ToString();
                            entry.Quantity = reader["Quantity"].ToString();
                            usageHistory.Add(entry);
                        }
                    }
                }

                // --- BRAIN OF THE DASHBOARD: Calculate Analytics before returning ---
                ViewBag.PredictiveReports = predictiveReports;
                ViewBag.History = usageHistory;

                // Dynamic Chart Counts
                ViewBag.EconomyCount = predictiveReports.Count(r => r.SystemPrediction.Contains("Economic"));
                ViewBag.IndustryCount = predictiveReports.Count(r => r.SystemPrediction.Contains("Industrial"));
                ViewBag.BusinessCount = predictiveReports.Count(r => r.SystemPrediction.Contains("Business"));
                ViewBag.GeneralCount = predictiveReports.Count(r => r.SystemPrediction.Contains("General"));

                // High Priority Forecast Logic
                ViewBag.MostNeeded = predictiveReports.Any()
                    ? predictiveReports.GroupBy(r => r.SystemPrediction)
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
                                Id = (int)id,
                                FullName = reader["FullName"].ToString(),
                                Position = reader["POSITION"].ToString(),
                                Role = reader["Role"].ToString()
                            };
                        }
                    }
                }

                if (userProfile == null) return RedirectToAction("Login");

                string sql = "SELECT * FROM PlanningInput WHERE faculty_id = @id ORDER BY planning_id DESC";
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
        public IActionResult SubmitPlanningData(int facultyId, string resourceRequest)
        {
            string input = resourceRequest.ToLower();

            // 1. Scoring Vectors (Relational Weights)
            double economy = 0, industry = 0, business = 0, tech = 0;

            // --- ECONOMIC & MARKET CONTEXT ---
            if (input.Contains("inflation") || input.Contains("market") || input.Contains("price")) economy += 3.5;
            if (input.Contains("supply") || input.Contains("demand") || input.Contains("cost")) economy += 2;

            // --- INDUSTRIAL & MANUFACTURING CONTEXT ---
            if (input.Contains("production") || input.Contains("factory") || input.Contains("raw material")) industry += 3.5;
            if (input.Contains("automation") || input.Contains("supply chain") || input.Contains("logistics")) industry += 2;

            // --- BUSINESS & REVENUE CONTEXT ---
            if (input.Contains("profit") || input.Contains("revenue") || input.Contains("competitor")) business += 3.5;
            if (input.Contains("client") || input.Contains("marketing") || input.Contains("growth")) business += 2;

            // --- TECHNICAL CONTEXT ---
            if (input.Contains("computer") || input.Contains("software") || input.Contains("server")) tech += 3.5;

            // 2. The Decision Matrix
            string prediction, suggestion, priority = "Normal";

            if (economy > industry && economy > business)
            {
                prediction = "Macro-Economic Volatility Forecast";
                suggestion = "Hedge against price increases and audit procurement contracts immediately.";
                priority = "High (Market Risk)";
            }
            else if (industry > business)
            {
                prediction = "Industrial Operational Bottleneck";
                suggestion = "Optimize supply chain logistics and investigate production automation.";
                priority = "Critical (Operational)";
            }
            else if (business > 1.5)
            {
                prediction = "Business Revenue Strategy Shift";
                suggestion = "Perform a SWOT analysis and focus on high-margin client retention.";
                priority = "Standard (Strategic)";
            }
            else
            {
                prediction = "General Institutional Need";
                suggestion = "Standard review. No immediate financial or industrial threat detected.";
            }

            // 3. Save everything to the database (Using the new columns)
            using (MySqlConnection conn = new MySqlConnection(connString))
            {
                conn.Open();
                string sql = "INSERT INTO planninginput (faculty_id, resource_request, planning_period, status, ai_suggestion, priority_level) " +
                             "VALUES (@fid, @req, '2026-FY', @pred, @sugg, @pri)";
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@fid", facultyId);
                    cmd.Parameters.AddWithValue("@req", resourceRequest);
                    cmd.Parameters.AddWithValue("@pred", prediction);
                    cmd.Parameters.AddWithValue("@sugg", suggestion);
                    cmd.Parameters.AddWithValue("@pri", priority);
                    cmd.ExecuteNonQuery();
                }
            }

            return RedirectToAction("UserDashboard", new { id = facultyId });
        }


        [HttpPost]
        public IActionResult GenerateForecast()
        {
            TempData["Message"] = "AI Analytics Engine Re-Synced Successfully.";
            return RedirectToAction("AdminDashboard");
        }


        public IActionResult ContactAdmin()
        {
            // You can pull this from a database later, but for now, we'll hardcode it
            ViewBag.AdminEmail = "gandalusaoryza9@gmail.com";
            ViewBag.AdminPhone = "+63 981 711 4634";
            ViewBag.OfficeHours = "Monday - Friday, 8:00 AM - 5:00 PM";

            return View();
        }
        public IActionResult ViewRecords(int id)
        {
            return RedirectToAction("UserDashboard", new { id = id });
        }



        public IActionResult Logout() => RedirectToAction("Login");
    }
}