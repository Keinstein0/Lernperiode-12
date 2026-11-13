export const projects = [
 {slug:'website-redesign',name:'Website redesign',description:'Improve the application interface, navigation and component system across the workspace.',status:'Active',updated:'2 hours ago',tasks:12,open:5,agents:3,repository:null,owner:'Workspace',created:'5 Nov 2026',details:'Design and implement a consistent, accessible interface. Establish page structure and navigation before connecting the frontend to the platform services.'},
 {slug:'research-pipeline',name:'Research pipeline',description:'Coordinate research, source evaluation and structured reporting.',status:'Active',updated:'5 hours ago',tasks:8,open:3,agents:4,repository:null,owner:'Workspace',created:'3 Nov 2026',details:'Collect supporting material, review evidence and organise findings into clear reports that can be referenced by other projects.'},
 {slug:'code-review',name:'Code review',description:'Review changes, assess quality and track improvements.',status:'Idle',updated:'Yesterday',tasks:5,open:2,agents:2,repository:null,owner:'Workspace',created:'29 Oct 2026',details:'Review code changes, maintain consistent standards and document any identified issues for follow-up.'},
 {slug:'documentation',name:'Documentation',description:'Maintain architecture notes, developer guidance and technical references.',status:'Idle',updated:'2 days ago',tasks:7,open:4,agents:2,repository:null,owner:'Workspace',created:'21 Oct 2026',details:'Keep implementation guidance and project decisions organised for everyone working across the platform.'}
];
export const tasks = [
 {id:24,title:'Audit existing components',status:'Closed',project:'website-redesign',assignee:'Review agent',date:'12 Nov 2026'},
 {id:25,title:'Build reusable navigation components',status:'In progress',project:'website-redesign',assignee:'Code agent',date:'13 Nov 2026'},
 {id:26,title:'Research accessibility patterns',status:'Closed',project:'website-redesign',assignee:'Research agent',date:'11 Nov 2026'},
 {id:27,title:'Document interface guidelines',status:'Open',project:'website-redesign',assignee:'Docs agent',date:'13 Nov 2026'},
 {id:28,title:'Review project page hierarchy',status:'Open',project:'website-redesign',assignee:'Review agent',date:'13 Nov 2026'},
 {id:29,title:'Collect reference sources',status:'In progress',project:'research-pipeline',assignee:'Research agent',date:'12 Nov 2026'},
 {id:30,title:'Summarise research findings',status:'Open',project:'research-pipeline',assignee:'Docs agent',date:'13 Nov 2026'},
 {id:31,title:'Evaluate outstanding changes',status:'Open',project:'code-review',assignee:'Review agent',date:'12 Nov 2026'},
 {id:32,title:'Update developer documentation',status:'Open',project:'documentation',assignee:'Docs agent',date:'11 Nov 2026'}
];
export const agents = [
 {name:'Research agent',id:'research-01',department:'Research',status:'Online',project:'Research pipeline'},
 {name:'Docs agent',id:'docs-05',department:'Research',status:'Online',project:'Documentation'},
 {name:'Code agent',id:'code-02',department:'Development',status:'Working',project:'Website redesign'},
 {name:'Review agent',id:'review-03',department:'Development',status:'Online',project:'Code review'},
 {name:'Test agent',id:'test-04',department:'Quality assurance',status:'Offline',project:'Website redesign'},
 {name:'Planning agent',id:'plan-06',department:'Coordination',status:'Working',project:'Research pipeline'}
];
