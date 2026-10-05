# Meeting transcript — meeting 1001

- Source: `meeting 1001.mp3`
- Model: faster-whisper `D:\Work\Development\12-GMP Production Platform\references\meeting-1002\models\faster-whisper-small.en` (cpu/int8)
- Language: en (1.000)
- Duration: 34.7 min

---

[00:02] Okay, so we, we look at that. I look on your video also. Thank you, that was very nice.

[00:18] Oh, Marcin is with us. Hi Marcin. Hi. So Brian, I would have question because that's what you sent us. It's, I would say very basic for now.

[01:00] I understand that it was a short time, right? And I'm just wondering if this, we could achieve, you know, the style exactly the same as, you know, I showed you.

[01:15] This is very important because for now the dimension lines look different than the, you know, the text look different.

[01:29] I'm just, you know, because I'm not that computer savvy, so I'm not sure. However, when I was doing this by myself, I was able to achieve the same style for the dimensions, the same style for the text.

[01:45] I can open up this in the second and I show you. And this would be the, you know, the most important question because in general, we know that it's achievable, I mean, to create this tool.

[02:01] The question is, you know, if this can be exactly as I showed you this example, or it's going to be different, it's going to look different, you know what I mean?

[02:17] I don't know if Marcin you can describe it somehow, maybe what we mean by this, but that's, because our client is very picky, so it's not only that the information that we have needs to be correct, they need to be correct, but it needs to look like this ticket that I gave you, basically, the style.

[02:41] Does this make sense?

[02:47] Yeah, totally makes sense. So let me explain.

[02:55] So I think that is really the key point, I think.

[03:01] So the goal is not to give you a different looking automatic ticket. So the goal is to reproduce the Tinder ticket standard as closely as possible.

[03:18] So that is actually why I choose to generate the final drawing natively in AutoCAD instead of simply exporting rabbit sheets.

[03:31] So I think from now, the POC version, that it starts from a copy of your approved DWS file, so we are using the actual type blocks and layers and dimension styles and layouts and existing blocks,

[03:57] rather than creating something that only looks similar. So I think these are still differences today.

[04:07] For example, I don't have the Tinder's original, you know, SHX fonts or, you know, final print of, you know, CTV file and also some, you know, details and conditions, specific sections still need to be built.

[04:33] But I think regarding the style, technically, I believe we can get very close to the exact style.

[04:45] You shoot me and I treat the visual standard as the acceptance requirement, not just a preference. Yeah, I'm sure we can do this, I think.

[05:01] Okay, I'm going to share my screen. And this is the example of what I achieved.

[05:11] And I stopped, it was like two months ago, probably, and I didn't have time. So this is exactly the same, the style for the dimensions for the text,

[05:29] for the top view side view. It sucks up the, you know, the elements and mark it up the same, I mean, hatchet the same way as we have it on our drawings.

[05:45] Here is the second view, like a bottom form view, also with the already with dimension generated, some running dimensions and other plates that are in the bottom surface of the wall, plus the table generated, you know, based on this.

[06:08] And so I think it's, it's probably achievable. I, as I said, we don't have enough time to play with this. And

[06:22] what about the timeframe and cost of this? Let's say for each model, for example, for the walls only, like those, how do you assume or if it would be

[06:37] how much time it would take you to, to prepare something, there's, there's no such a big, you know, rush or something, but estimate, estimate time.

[06:53] That would look similar to the final ticket. Right.

[07:00] What do you think? Like a week, two weeks, month, half a year, a year?

[07:05] Let me think. So this is a POS version, it's just for wall, wall elements. So I think I'm not half a year or year for the wall panel piece. So based on what I've, what I've learned from the model and this POS version,

[07:46] I would estimate around three or six weeks to get the wall workflow to a solid production ready first version. So I don't want to say one or two weeks because the last part is actually the typical part.

[08:08] So, I mean the condition specific sections and, you know, finished details and reinforcement rules and, you know, bomb rules and getting the graphics are very close to client standard.

[08:24] And those are the main gaps in the POS today. So I'd probably structure it in milestones. First is,

[08:35] finish one reference wall, like the sample you just shared properly, and then test different for conditions like openings and, you know, cobbles and pockets and, you know, different embeds.

[08:54] And finally, having it put several real panels. So I think if you need to complete this, you know, deliverable as soon as possible.

[09:13] I can, you know, do overall and also including weekend. So I think I can develop this, you know, project as soon as possible.

[09:33] As quickly as possible, I think on my side. So, yeah.

[09:42] I have a question when you mentioned about the rebar rule. By this, you mean that the rebar would need to be modeled in the main model or that would be some sort of the rule that

[10:07] would be applied only on the ticket and as a, let's say, I don't know, 2D graphics, for example. The reason why I'm asking is because if I know that I'm going deep, too deep into the details.

[10:31] But I think it's important, too, when you're talking about that rebar rule, and if those rebars would need to be developed inside that 3D model and modeled them as a 3D shape

[10:58] that will significantly, how to say, overwhelm the model itself, meaning it will grow with weight. This means that it may be heavier to work with, to start editing, etc, etc.

[11:26] So when you mentioned about the rebar rule, you meant that all the rebars, the regular bars, strands, etc, would need to be, or meshes, rebar meshes would need to be physically placed in the model.

[11:44] Or you meant something, that rebar rule, whatever that is, but when the ticket will be done by itself, that rebar rule will, I don't know, layout.

[12:02] For example, here, what we see on the screen, you have that rebar rule that you click and that rebar rule will place the rebar independently from the main model on that ticket.

[12:29] So Marcin's question, basically, is if, for example, here in AutoCAD, we put the line that shows the rebar, right?

[12:49] On the ticket. The question is, if this needs to be already on the model, or we can put some kind of parameters and, you know, that we can, that program would recognize, okay, there is an opening, I could put, you know, rebars around or something like that.

[13:11] That's the in AutoCAD, already not in the model.

[13:16] Or what, what did you mean by rebar rule?

[13:22] Oh, yeah. So let me, let me explain. So as the second option, I mean, I don't mean that we need to model every bar and, you know, strand and mesh physically in the main rabbit model.

[13:46] So actually, I'd prefer not to do that for exactly the reason you mentioned, it can make the production model unnecessarily heavy and more difficult for your team to work with.

[14:03] So, in the current POC, the wall reinforcement is not modeled in rabbit. The automation, you know, reach the panel geometry and parameters, and then the rebar rule determines what reinforcement should appear on the shop ticket.

[14:24] So for example, it can calculate bar size and length and quantity and lap and spacing and location and then throw that reinforcement directly in AutoCAD as ticket graphics.

[14:39] So the rabbit stays, you know, relatively clean. So the ticket generation engine applies and the reinforcement rules independently when the ticket is created.

[14:53] So what I need from you is the engineering logic for this wall condition. Wood bars and mesh and strands and trim bars and spacing and cover and, you know, opening reinforcement, etc.

[15:09] So it should be shown once we define these rules, the software can generate them automatically without physically adding all the reinforcement to the rabbit model.

[15:24] So it doesn't need to already exist as rebar in the rabbit model. So, for example, if rabbit gives us the actual wall outline and the opening geometry, the program can recognize like, okay, there is an opening here, then our rebar rule engine can say that

[15:51] wood reinforcement is required around the opening, like, you know, bar size and offset from the opening and extension length and quantity and lap and so on.

[16:04] And then those bars can be drawn directly on the AutoCAD shop ticket as 2D fabrication graphics. So we don't need to create those bars as physical authority in a rapid elements. I think that is actually how I'd prefer to build this.

[16:26] The important thing I need from you and your engineering team is the rule itself. So, for example, for this type and size of a print, put these bars at these locations for a different condition using another rule.

[16:44] So, a rabbit gives us the geometric parameters and the ticket engine applies your engineering reinforcement rules. So that keeps the rabbit model much lighter while, you know, still, you know, producing the detailed shop ticket you need.

[17:02] And this feature is optional, right? Because we can do this or we can abandon this and do the reinforcement by hand, right? You know what I mean? That we can achieve basically something like I'm showing here and put the reinforcement by drafters by hand.

[17:29] I think what Greg is saying, I mean, in which direction he's leaning to is that that rebar tool or that rebar rule would be good to have. However, the first step would be to have everything without the rebar because it looks like that would be the fastest way.

[17:51] And all those engineering stuff like what is the size of the bar, the cover, the clearance, the band, the appropriate radius of the band, etc., etc., etc.

[18:05] This is a lot of information that there's no point that it would be no point for now holding that tool to export all those elements and be ready for the drafters to drop in some rebars and move.

[18:23] And that rebar ruler or how did you call it? That thing with the rebars we could do in the later stage. That would be possible, right? This is what you meant, right?

[18:47] Exactly, because I'm wondering if this would be even possible to cover all the cases that we have. Sometimes it's so many different cases, so many different rules.

[18:59] I was thinking about Meantime, Greg, and I think it couldn't be fixed. Let's say...

[19:13] I know what you want to say. It's not going to recognize automatically. It would have to be like a window.

[19:19] Yeah, and you are including your data, what you want to have. For example, I want to have for this one specifically, I want to have clearance, for example, three inches, right? But somewhere, three quarters.

[19:31] On the end of the day, putting this information for each ticket, it may take more time than it was copied over and over and do it by hand.

[19:40] That's why I'm asking if we can first start with the version that we have all the place, everything, everything, except the reinforcement, because that could be a pretty complex and pretty big step.

[20:00] Which doesn't mean it cannot be done, because we've already seen that. It's possible. We've seen Revit models with implementing all the rebars in 3D into the beam and walls and everything.

[20:17] And just from our perspective, for now...

[20:22] For us, even the module that would do something similar, like you can see on my screen, without reinforcement, it would be already pretty satisfactory for us. It would be good enough.

[20:40] So I have another question, though, because you have these details here, right? And on the page number three that are shown here, are they live sections or they are just like, you know, dead?

[20:55] Meaning, if something changes on section A, let's say, if something changes on the ticket itself, is it going to automatically change on these details?

[21:11] Yeah, of course.

[21:12] I'm sorry, say again?

[21:19] Yeah, it can be changed automatically.

[21:25] Yeah, so, yeah, let me explain. So, just a sec.

[21:48] So, the first, I think, I can understand what you are saying right now. So, first time, I completely agree on the reinforcement. So we can make that a later phase.

[22:12] So, for the first production version, we can regenerate the wall geometry and openings and embeds and dimensions and sections and form and details and the correct ticket presentation and your drafter as the reinforcement manually.

[22:32] So, that will reduce the complexity and get you useful automation much faster.

[22:39] So, for your section question, in this current POC, you know, these are not, you know, live rapid sections. So, they are generated programmatically in AutoCAD from the information extracted from rapid.

[23:03] But they are also not just static copied details. So, when you generate the ticket again, the program reads the current panel data and regenerates the sections.

[23:16] So, for example, if the panel sequence changes from 9 inches to 10 inches and that information is in rapid, the regenerated section can reflect the automatically.

[23:31] So, for complex conditions, like, you know, pockets, openings, you know, installation changes, we still need to build the condition specific section logic. That is one of the remaining pieces.

[23:49] Okay.

[23:52] Fran, what about, because we know it's going to take a few weeks, what about pricing? For example, the version first the step without the reinforcement and version, let's say with reinforcement.

[24:09] How would you think how much it may cost?

[24:19] Hey, Greg, may I have a question?

[24:23] Sure, go ahead.

[24:24] Oh, yeah. So, what is the more important thing on your side, between the timeframe and budget?

[24:36] You know, the budget is a little bit more important because it's not that cheap, right? Time is not that bad, you know, a few days, weeks, either side, either way, it's okay.

[24:53] So, could you share with me your target timeframe?

[25:07] I think that would you propose it's completely acceptable.

[25:13] Okay. So, based on what I've seen now, I would separate it into two stages. So, the first version without reinforcement automation, I think we can complete about two or three weeks.

[25:42] I think so that focus and getting the wall tickets much closer and to production and panel geometry and openings and embeds and, you know, dimensions and condition specific sections and shit, two details and, you know, bomb and tight blocks and matching the tindle drafting standard.

[26:08] So, I think, if, of course, as I mentioned before, I can, you know, do overwork, including the weekend, so I can, you know, finish the development as soon as possible.

[26:31] So, I think, but depending on how many different work conditions we need to support. So, yeah, this is dependent on, you know, this kind of wall types, what conditions we need to support.

[26:53] Yes, but that two, three weeks, you would be for the first, let's say, the first stage of the many other elements and stages for this two, three weeks to complete that phase, how much you think it would cost.

[27:16] Oh, yeah. So, let me think. So, my hourly rate is 25 to, you know, $35 per hour. So, based on my, you know, hourly rate, I think I'm the first version without reinforcement automation.

[27:48] It can, you know, estimate roughly about, you know, 3k or 2, 5k, I think the first version and also the next, the separate phase, I mean, the reinforcement automation phase.

[28:21] This interface, depending on the complexity, I'd roughly estimate about one or two weeks. So, the acoustic is about, you know, 1.5 to 3k, I think.

[28:44] But I think, you know, this budget, I mean, I'm really flexible. So, all the, you know, budget and, you know, timeframe.

[28:59] These things are negotiable. So, depending on the prospect and complexity and, you know, the requirement, it can be, you know, negotiable.

[29:13] And also, I'm also more interested in, you know, long-term collaboration with you. So, yeah, that's it. Yeah.

[29:28] Okay. Thank you.

[29:32] Yeah. Okay. So, because I have another meeting in a minute. So, I think we know everything. Let us talk it to, yeah, talk it to, we decide which version, but probably, if we would go, that would be probably just without reinforcement at first step, but let us discuss about this a little bit.

[29:55] And I will get back to you. And in case we decide to go with that, I send you the drawings as an example that I sent you, I think, before, but just to be sure that on the end of the process, you know what we're supposed to achieve, right?

[30:13] And we agree to this, and we have, like, agreement in the email, right? And so, everything would be perfect, you know, clear from both sides, for both sides.

[30:28] And we would go from there, okay?

[30:32] Okay. Yeah, it would work for good.

[30:36] Yeah, no problem. So, I think if you, you know, send a more clearer document about the development of the project requirement, I can, you know, provide the more exact, more precise the timeframe and, you know,

[31:00] I think, you know, you listed correctly that, you know, the style, the dimensions, the embeds, exactly what you said, I just would prefer to have it in the email, so we are on the same page, right?

[31:19] Yeah.

[31:20] What are we looking for? And we expect in this, and you know that we expect in this, and not more, not less, right?

[31:27] Yeah.

[31:29] So, we cover your side, and we cover our side, in this case, right?

[31:33] Yeah.

[31:34] Okay.

[31:35] Okay. So, let us talk about matching, and I will get back to you maybe Monday, so we can still talk about that, because we're so busy that we're working, like, 15 hours a day.

[31:48] Yes. Let's say sometime during next week, because Mondays might be crazy, and as you said, but we will get back to you, Brian, okay?

[31:59] Okay. Yeah. So, I think I'm starting with the reinforcement automation is the practical first step, I think, and we can focus on getting, you know, more whole wall ticket, like, you know, geometry and openings and embeds and dimensions and sections and, you know, etc.

[32:21] Especially the Tindall drawing style as close as possible to your approved examples, and then reinforcement can remain manual for now and become a separate phase later, if it makes business sense.

[32:37] And also, please send me the representative and drawings you want us to use the final acceptance examples before development starts again prepare the clear reading scope, showing exactly what phase one includes our remains manual and the expected deliverables and type and narrative cost.

[33:00] Yeah. And before we start, I will put everything clearly in writing, what finishes wall ticket should look like, and they require drawing style and all of etc.

[33:15] And also, I will clearly in a state the reinforcement automation is outside the first phase, and you and machine can review the scope against your example drawings.

[33:29] And once we both approve it, and that becomes a real target, and, you know, exactly what you are paying for, and I know exactly what I need to deliver.

[33:41] I think this is more in a professional and transparent. And also, that's what I'm talking about. Yeah, I'm really, really interested in, you know, longer term collaboration and to build this production is really in a value in the AEC industry.

[34:03] So, yeah, no problem about Monday, I understand you're both really busy. And so discuss it when you have time and just get back to me sometime next week.

[34:16] Yes, we will. Thank you so much, Brian. Thank you.

[34:21] Good meaning. See ya. Bye bye. Have a great day. See you next week.

[34:27] Thank you.
