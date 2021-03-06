﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using esg.Models;

namespace esg.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class UserController : Controller
    {
        string connString = "server=rm-bp1o13lfefc6t7z98io.mysql.rds.aliyuncs.com;database=esg_information_database;uid=super_esg;pwd=esg123456";

        [HttpPost]
        public int CreateNewCustomer([FromBody]User user)//创建新用户
        {
            int id;
            using (MySqlConnection con = new MySqlConnection(connString))
            {
                con.Open();

                //插入数据库失败后会报错崩溃，所以用代码判断边界条件
                //判断是否存在公司ID
                string sql = "select * from company where com_id='" + user.CompanyId + "'";
                MySqlCommand cmd = new MySqlCommand(sql, con);
                MySqlDataReader reader = cmd.ExecuteReader();
                if (!reader.HasRows)
                {
                    reader.Close();
                    return 0;
                }
                reader.Close();

                //判断用户名和邮箱是否重复
                sql = "select account,email from user where account=@act or email=@eml";
                cmd.CommandText = sql;//更新一下sql语句
                cmd.Parameters.Add(new MySqlParameter("@act", user.UserAccount));
                cmd.Parameters.Add(new MySqlParameter("@eml", user.Email));
                reader = cmd.ExecuteReader();
                if (reader.HasRows)
                {
                    reader.Close();
                    return 0;
                }
                reader.Close();

                sql = "insert into user(com_id,account,password,email,level) values (@com_id,@account,@pwd,@email,@lvl)";
                cmd.CommandText = sql;//更新一下sql语句
                cmd.Parameters.Add(new MySqlParameter("@com_id", user.CompanyId));
                cmd.Parameters.Add(new MySqlParameter("@account", user.UserAccount));
                cmd.Parameters.Add(new MySqlParameter("@pwd", user.UserPassword));
                cmd.Parameters.Add(new MySqlParameter("@email", user.Email));
                cmd.Parameters.Add(new MySqlParameter("@lvl", user.Level));
                cmd.ExecuteNonQuery();
                id = (int)cmd.LastInsertedId;
                con.Close();
            }
            return id;
        }

        [HttpDelete]//Delete请求
        public int DeleteCustomer(int UserId)//删除客户
        {
            using (MySqlConnection con = new MySqlConnection(connString))
            {
                con.Open();

                //判断该ID是否存在
                string sql = "select user_id from user where user_id='" + UserId + "'";
                MySqlCommand cmd = new MySqlCommand(sql, con);
                MySqlDataReader reader = cmd.ExecuteReader();
                if (!reader.HasRows)
                {
                    reader.Close();
                    return 0;
                }
                reader.Close();

                sql = "delete from user where user_id='" + UserId + "'";
                cmd.CommandText = sql;//更新一下sql语句
                cmd.ExecuteNonQuery();
                con.Close();
            }
            return 1;
        }

        [HttpPost]
        public FirstCreatedUser ViewFirstCustomerInfo([FromBody]int UserId)//查看本公司名称、已建立客户信息和子公司名称
        {
            FirstCreatedUser FCUser = new FirstCreatedUser();
            using (MySqlConnection con = new MySqlConnection(connString))
            {
                con.Open();

                //判断该ID是否存在
                string sql = "select user_id from user where user_id='" + UserId + "'";
                MySqlCommand cmd = new MySqlCommand(sql, con);
                MySqlDataReader reader = cmd.ExecuteReader();
                if (!reader.HasRows)
                {
                    reader.Close();
                    FCUser.ErrorCode = 0;
                    return FCUser;
                }
                reader.Close();
                FCUser.ErrorCode = 1;//存在该ID

                //查找本公司名称
                sql = "select name from company join user using(com_id) where user_id='"+UserId+"'";
                cmd.CommandText = sql;
                reader = cmd.ExecuteReader();
                while(reader.Read())
                {
                   FCUser.CompanyName = reader.GetString("name");
                }
                reader.Close();

                //查找本公司管理员和录入员
                sql = "select user_id,account,level from user where com_id=(select com_id from user where user_id=@UserId)";
                cmd.CommandText = sql;
                cmd.Parameters.Add(new MySqlParameter("@UserId", UserId));
                reader = cmd.ExecuteReader();
                while(reader.Read())
                {
                    string act = reader.GetString("account");
                    int num=reader.GetInt32("user_id");
                    if (reader.GetInt32("level") == 1)
                    {
                        FCUser.AdminAccount.Add(act);
                    }
                    else FCUser.DataAccount.Add(act);
                    FCUser.userID.Add(num);
                }
                reader.Close();

                //查找子公司名称
                sql = "select name from company where parent=(select com_id from user where user_id=@ui)";
                cmd.CommandText = sql;
                cmd.Parameters.Add(new MySqlParameter("@ui", UserId));
                reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    FCUser.ChildCompanyName.Add(reader.GetString("name"));
                }
                reader.Close();

                con.Close();
            }
            return FCUser;
        }

        [HttpPost]
        public CreatedChildUser ViewChildCustomer([FromBody]string CompanyName)
        //基于一级管理员查看已建立客户信息，再选择某个公司名称，返回子公司内的管理员和录入员账号
        {
            CreatedChildUser CCUser = new CreatedChildUser();
            using (MySqlConnection con = new MySqlConnection(connString))
            {
                con.Open();

                //查找本公司管理员和录入员
                string sql = "select user_id,account,level from user where com_id in (select com_id from company where name=@name)";
                MySqlCommand cmd = new MySqlCommand(sql, con);
                cmd.Parameters.Add(new MySqlParameter("@name", CompanyName));
                MySqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    int num=reader.GetInt32("user_id");
                    string act = reader.GetString("account");
                    if (reader.GetInt32("level") == 2)
                    {
                        CCUser.AdminAccount.Add(act);
                    }
                    else CCUser.DataAccount.Add(act);
                    CCUser.userID.Add(num);
                }
                reader.Close();

                con.Close();
            }
            return CCUser;
        }

        [HttpPost]
        public SecondCreatedUser ViewSecondCustomerInfo([FromBody]int UserId)//二级管理员查询已建立客户信息，返回本公司名称以及管理员和录入员的账号
        {
            SecondCreatedUser SCUser = new SecondCreatedUser();
            using (MySqlConnection con = new MySqlConnection(connString))
            {
                con.Open();

                //查找本公司管理员和录入员
                string sql = "select user_id,account,level from user where com_id in (select com_id from user where user_id=@userid)";
                MySqlCommand cmd = new MySqlCommand(sql, con);
                cmd.Parameters.Add(new MySqlParameter("@userid", UserId));
                MySqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    int num=reader.GetInt32("user_id");
                    string act = reader.GetString("account");
                    if (reader.GetInt32("level") == 1)
                    {
                        SCUser.AdminAccount.Add(act);
                    }
                    else SCUser.DataAccount.Add(act);
                    SCUser.userID.Add(num);
                }
                reader.Close();

                //查找本公司名称
                sql = "select name from company where com_id in (select com_id from user where user_id=@ui)";
                cmd.CommandText = sql;
                cmd.Parameters.Add(new MySqlParameter("@ui", UserId));
                reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    SCUser.CompanyName = reader.GetString("name");
                }
                reader.Close();

                con.Close();
            }
            return SCUser;
        }

        [HttpPost]
        public JsonResult login([FromBody] User user)
        {
            List<loginReturn> list = new List<loginReturn>();

            using (MySqlConnection conn = new MySqlConnection(connString))
            {
                conn.Open();
                list.Add(new loginReturn { ErrorCode = 0 });
                using (MySqlCommand cmd = new MySqlCommand())
                {
                    string sql = "select * from user";
                    cmd.Connection = conn;
                    cmd.CommandText = sql;
                    MySqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        if (user.UserAccount == reader.GetString(2))
                        {
                            //此时用户存在，密码错误；若为0，用户名不存在
                            list[0].ErrorCode = 1;
                            if (user.UserPassword == reader.GetString(3))
                            {
                                //密码正确，登陆成功
                                list[0].ErrorCode = 2;
                                list[0].UserId = reader.GetInt32(0);
                                list[0].Level = reader.GetInt32(5);
                                list[0].Comid = reader.GetInt32(1);
                                list[0].CompanyName = getCompanyName(reader.GetInt32(1));
                                reader.Close();
                                break;
                            }
                        }
                    }
                }
                conn.Close();
            }
            return Json(list);
        }
        //查看信息
        [HttpPost]
        public JsonResult viewPI([FromBody] int user_id)
        {
            List<viewPIReturn> list = new List<viewPIReturn>();

            using (MySqlConnection conn = new MySqlConnection(connString))
            {
                conn.Open();

                using (MySqlCommand cmd = new MySqlCommand())
                {
                    list.Add(new viewPIReturn { ErrorCode = 0 });
                    string sql = "select * from user";
                    cmd.Connection = conn;
                    cmd.CommandText = sql;
                    MySqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        if (user_id == reader.GetInt32(0))
                        {
                            list[0].ErrorCode = 1;
                            list[0].Level = reader.GetInt32(5);
                            list[0].Email = reader.GetString(4);
                            list[0].CompanyName = getCompanyName(reader.GetInt32(1));
                            reader.Close();
                            break;
                        }
                    }
                }
                conn.Close();
            }
            return Json(list);
        }

        //修改信息
        [HttpPut]
        public int changePI([FromBody] User user)
        {
            int error_code = 1;

            using (MySqlConnection conn = new MySqlConnection(connString))
            {
                conn.Open();

                using (MySqlCommand cmd = new MySqlCommand())
                {
                    string sql = "update user set email=@email where user_id=@user_id";
                    cmd.Connection = conn;
                    cmd.CommandText = sql;
                    cmd.Parameters.Add(new MySqlParameter("@email", user.Email));
                    cmd.Parameters.Add(new MySqlParameter("@user_id", user.UserId));
                    if (cmd.ExecuteNonQuery() != 1)
                    {
                        error_code = 0;
                    }

                }
                conn.Close();
            }
            return error_code;
        }
        //查找公司名称
        string getCompanyName(int com_id)
        {

            using (MySqlConnection conn = new MySqlConnection(connString))
            {
                conn.Open();

                using (MySqlCommand cmd = new MySqlCommand())
                {
                    string sql = "select * from company";

                    cmd.Connection = conn;
                    cmd.CommandText = sql;
                    MySqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        if (com_id == reader.GetInt32(0))
                        {
                            string temp = reader.GetString(2);
                            reader.Close();
                            return temp;

                        }
                    }
                }
                conn.Close();
            }
            return "";
        }

        
        [HttpPost]
        public AlluserResponse GetAllUser()
        {
            AlluserResponse response=new AlluserResponse();
            response.total=0;
            response.useritem=new List<userinfo>();

            Dictionary<int,List<userinfo>> FirstCom= new Dictionary<int, List<userinfo>>();
            Dictionary<int,List<int>> ChildCom = new Dictionary<int, List<int>>();
            List<int> comid=new List<int>();
            using (MySqlConnection con = new MySqlConnection(connString))
            {
                con.Open();

                //var temp_com_id=0;
                List<userinfo> temp_info =new List<userinfo>();

                string sql = "SELECT user_id,user.com_id,account,user.level,name FROM user JOIN company USING (com_id) ";
                MySqlCommand cmd = new MySqlCommand(sql, con);
                MySqlDataReader reader = cmd.ExecuteReader();
                while(reader.Read())
                {
                    userinfo temp=new userinfo();
                    temp.account=reader.GetString("account");
                    temp.com_id=reader.GetInt32("com_id");
                    temp.com_name=reader.GetString("name");
                    temp.level=reader.GetInt32("level");
                    temp.user_id=reader.GetInt32("user_id");
                    response.total++;
                    if(FirstCom.ContainsKey(temp.com_id))
                    {
                        FirstCom[temp.com_id].Add(temp);
                    }
                    else{
                        comid.Add(temp.com_id);
                        temp_info.Add(temp);
                        FirstCom.Add(temp.com_id,temp_info);
                        temp_info =new List<userinfo>();
                    }


                    // if(temp_com_id!=temp.com_id)
                    // {
                    //     if(temp_com_id==0)
                    //     {
                    //         temp_com_id=temp.com_id;
                    //         continue;
                    //     }
                    //     FirstCom.Add(temp_com_id,temp_info);
                    //     comid.Add(temp_com_id);
                    //     temp_com_id=temp.com_id;
                    //     temp_info=new List<userinfo>();                        
                    // }
                    // temp_info.Add(temp);
                }
                reader.Close();

                
                sql = "select com_id,parent from company where parent<>0";
                cmd.CommandText = sql;
                //List<int> temp_child=new List<int>();
                reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    int temp_key=reader.GetInt32("parent");
                    int temp_id=reader.GetInt32("com_id");
                    if(ChildCom.ContainsKey(temp_key))
                    {
                        ChildCom[temp_key].Add(temp_id);
                    }
                    else{
                        List<int> temp_child=new List<int>();
                        temp_child.Add(temp_id);
                        ChildCom.Add(temp_key,temp_child);
                    }
                }
                reader.Close();

                con.Close();
            }
            int com_num=comid.Count;
            Dictionary<int,bool> ifuse=new Dictionary<int, bool>();
            for (int i=0;i<com_num;i++)
            {
                ifuse.Add(comid[i],false);
            }
            for(int i=0;i<com_num;i++)
            {
                if(ifuse[comid[i]])
                {
                    continue;
                }
                List<userinfo> insertinfo=FirstCom[comid[i]];
                for (int x=0;x<insertinfo.Count;x++)
                {
                    response.useritem.Add(insertinfo[x]);
                }
                if(!ChildCom.ContainsKey(comid[i]))
                {
                    continue;
                }
                List<int> insertcom=ChildCom[comid[i]];
                for(int x=0;x<insertcom.Count;x++)
                {
                    if(!FirstCom.ContainsKey(insertcom[x]))continue;
                    List<userinfo> insertinfos=FirstCom[insertcom[x]];
                    for (int y=0;y<insertinfos.Count;y++)
                    {   
                        response.useritem.Add(insertinfos[y]);
                    }
                    ifuse[insertcom[x]]=true;
                }
            }


            return response;
        }

        


        public class loginReturn
        {
            public int ErrorCode { get; set; }
            public int UserId { get; set; }
            public int Level { get; set; }
            public string CompanyName { get; set; }
            public int Comid{ get; set; }
        }

        public class viewPIReturn
        {
            public int ErrorCode { get; set; }
            public int Level { get; set; }
            public string CompanyName { get; set; }
            public string Email { get; set; }
        }


        public class userinfo{
            public int user_id{get; set;}

            public int com_id{get; set;}

            public string account{get; set;}

            public int level{get; set;}

            public string com_name{get; set;}
        }

        public class AlluserResponse
        {
            public int total{get; set;}

            public List<userinfo> useritem{get; set;}
        }
    }
}
