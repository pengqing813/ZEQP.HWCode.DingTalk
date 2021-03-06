﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ZEQP.WebHook.Robot.Models;

namespace ZEQP.WebHook.Robot.Service
{
    public class GitHubService
    {
        private HttpClient Client { get; }
        private IConfiguration Config { get; }
        private ILogger<GitHubService> Logger { get; }
        public GitHubService(HttpClient client, IConfiguration config, ILogger<GitHubService> logger)
        {
            this.Client = client;
            this.Config = config;
            this.Logger = logger;
        }
        public async Task SendDingTalkMarkdown(GitHubPushModel model)
        {
            this.Logger.LogInformation($"请求数据：{Environment.NewLine}{model.ToJson()}");
            var config = this.Config.GetSection("DingTalkConfig:Default").Get<DingTalkConfig>();
            var repositoryName = model.repository.name;
            var repConfig = this.Config.GetSection($"DingTalkConfig:{repositoryName}").Get<DingTalkConfig>();
            if (repConfig != null) config = repConfig;
            this.Logger.LogInformation($"配置：{repConfig.ToJson()}");
            var resModel = this.ConvertMD(model);
            var resJson = resModel.ToJson();
            this.Logger.LogInformation($"发送数据：{Environment.NewLine}{resJson}");
            var timestamp = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            var signHMACSHA256 = $"{timestamp}\n{config.Secret}".ToHMACSHA256(config.Secret);
            var signBase64 = signHMACSHA256.ToBase64();
            var sign = Uri.EscapeDataString(signBase64);
            var result = await this.Client.PostAsync($"/robot/send?access_token={config.Token}&timestamp={timestamp}&sign={sign}", new StringContent(resJson, Encoding.UTF8, "application/json"));
            var resultBody = await result.Content.ReadAsStringAsync();
            this.Logger.LogInformation($"返回数据：{Environment.NewLine}{result}{Environment.NewLine}{resultBody}");
        }
        public DingTalkResModel ConvertMD(GitHubPushModel model)
        {
            var result = new DingTalkResModel();
            result.msgtype = "markdown";
            var md = new DingTalkResMarkdown();
            md.title = $"{model.repository.name}源代码提交";
            var mdSb = new StringBuilder();
            foreach (var commit in model.commits)
            {
                mdSb.AppendLine($"# {commit.message}{Environment.NewLine}");
                mdSb.AppendLine($"> [{commit.author.name} {commit.timestamp}]({commit.url}){Environment.NewLine}");
            }
            mdSb.AppendLine($"> *Repository [{model.repository.name}]({model.repository.url})*{Environment.NewLine}");
            mdSb.AppendLine($"> *Organization [{model.organization.login}](https://github.com/{model.organization.login})*{Environment.NewLine}");
            md.text = mdSb.ToString();
            result.markdown = md;
            return result;
        }
    }
}
