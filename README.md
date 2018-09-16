# formsubmit
F# serverless form submit service

Sends email & SMS via AWS services.
Needs a verified email address for the source field.

https://xxxx.execute-api.us-east-1.amazonaws.com/production/message?email=xxx@gmail.com&phone=+xx%20xxxxxxxxxxx

Body with form fields as application/json. No nested records or lists

```json
{ "name" : "xxxxx",
  "age" : 30,
  "email" : "xxx@xxx.com",
  "message" : "Hi there.",
}
```
