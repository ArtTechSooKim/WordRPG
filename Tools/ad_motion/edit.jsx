// 게임 광고 모션그래픽 (Higgsedit — Higgsfield 샌드박스에서 실행, 방법은 Docs/작업메모.md #154)
// 배경은 앱스토어 스크린샷을 임시로 씀 → 사용자 녹화 영상을 받으면 Game()의 그림을 영상으로 바꾸고 시간을 맞춤
const NAVY="#121424", PANEL="#21263D", GOLD="#FFD140", WHITE="#F6F7FF", GREEN="#40BF66", RED="#FF5A52", OUTLINE="#0B0C18", DIM="#A6B3D1";
const W=1080,H=1920;
function rng(seed){let a=seed>>>0;return()=>{a=(a+0x6D2B79F5)>>>0;let t=a;t=Math.imul(t^(t>>>15),t|1);t^=t+Math.imul(t^(t>>>7),t|61);return((t^(t>>>14))>>>0)/4294967296;};}
function Stamp({s,cx,cy,size=120,color=WHITE,at,dur,w=1040,rot=0,from=2.1,sw}){
  const h=Math.round(size*1.5), stroke=sw??Math.max(10,Math.round(size*0.15));
  return <frame x={cx-w/2} y={cy-h/2} width={w} height={h} layout="none" origin="center" at={at} duration={dur}
    motion={dur>0.45?{enter:{from:{scale:from,rotation:rot},duration:0.2,easing:{kind:"overshoot",amount:1.5}},exit:{to:{opacity:0,scale:1.08},duration:0.1,anchor:"end"}}:{enter:{from:{scale:from,rotation:rot},duration:Math.min(0.2,dur*0.6),easing:{kind:"overshoot",amount:1.5}}}}>
    <text x={0} y={0} width={w} height={h} align="center" fontFamily="Jua" fontSize={size} lineHeight={1.3} color={OUTLINE} strokeColor={OUTLINE} strokeWidth={stroke}>{s}</text>
    <text x={0} y={0} width={w} height={h} align="center" fontFamily="Jua" fontSize={size} lineHeight={1.3} color={color}>{s}</text>
  </frame>;
}
function Pill({s,cy,at,dur,size=56,color=NAVY,bg=GOLD,w=820,h=112,from=-1100}){
  return <frame x={(W-w)/2} y={cy-h/2} width={w} height={h} layout="none" at={at} duration={dur}
    motion={{enter:{from:{x:from},duration:0.22,easing:{kind:"overshoot",amount:1.1}},exit:{to:{x:-from},duration:0.14,anchor:"end"}}}>
    <rect x={0} y={0} width={w} height={h} radius={h/2} fill={bg} shadow={{y:10,blur:0,color:OUTLINE}}/>
    <text x={0} y={(h-size*1.3)/2} width={w} height={h} align="center" fontFamily="Jua" fontSize={size} lineHeight={1.3} color={color}>{s}</text>
  </frame>;
}
function Burst({cx,cy,at,n=26,seed=1,colors=[GOLD,WHITE],spread=460}){
  const r=rng(seed),out=[];
  for(let i=0;i<n;i++){const a=r()*Math.PI*2,d=spread*(0.35+r()*0.65),s=[14,20,28,36][Math.floor(r()*4)],dx=Math.cos(a)*d,dy=Math.sin(a)*d;
    out.push(<rect key={`b${seed}-${i}`} x={cx-s/2} y={cy-s/2} width={s} height={s} fill={colors[i%colors.length]} at={at} duration={0.62}
      animate={[{property:"offsetX",from:0,to:dx,duration:0.6,easing:"ease-out"},{property:"offsetY",keyframes:[{at:0,value:0,easing:"ease-out"},{at:0.32,value:dy},{at:0.6,value:dy+150}]},{property:"opacity",keyframes:[{at:0,value:1},{at:0.38,value:1},{at:0.6,value:0}]}]}/>);}
  return out;
}
const Flash=({at,c="#FFFFFF",o=0.85,d=0.2})=><rect x={0} y={0} width={W} height={H} fill={c} at={at} duration={d+0.02} animate={[{property:"opacity",keyframes:[{at:0,value:o},{at:d,value:0}]}]}/>;
function Slash({cx,cy,at,rot=-32,c="#FFFFFF",len=1250,th=30}){
  return <rect x={cx-len/2} y={cy-th/2} width={len} height={th} fill={c} at={at} duration={0.2}
    animate={[{property:"rotation",from:rot,to:rot,duration:0.18,easing:"linear"},{property:"scaleX",keyframes:[{at:0,value:0.1},{at:0.07,value:1},{at:0.18,value:1}]},{property:"scaleY",keyframes:[{at:0,value:1},{at:0.07,value:1},{at:0.18,value:0.02}]}]}/>;
}
function Wipe({at,seed=7}){
  const r=rng(seed),out=[],cols=9,rows=16,sz=120;
  for(let y=0;y<rows;y++)for(let x=0;x<cols;x++){const d=(y/rows)*0.14+r()*0.07,g=r()<0.13;
    out.push(<rect key={`w${seed}-${x}-${y}`} x={x*sz} y={y*sz} width={sz} height={sz} fill={g?GOLD:NAVY} at={at+d} duration={0.3}/>);}
  return out;
}
function Game({h,at,dur,y,hits=[],zoom=[1,1.035]}){
  const kx=[{at:0,value:0}],ky=[{at:0,value:0}];
  for(const t of hits){const b=t-at;[[0,0],[0.02,26],[0.06,-22],[0.1,16],[0.14,-10],[0.18,5],[0.22,0]].forEach(([o,v],i)=>{kx.push({at:b+o,value:v});ky.push({at:b+o,value:i%2?-v*0.7:v*0.7});});}
  const a=[{property:"scale",from:zoom[0],to:zoom[1],duration:dur-0.01,easing:"linear"}];
  if(hits.length){kx.push({at:dur-0.01,value:0});ky.push({at:dur-0.01,value:0});a.push({property:"offsetX",keyframes:kx},{property:"offsetY",keyframes:ky});}
  return <frame x={0} y={0} width={W} height={H} layout="none" clip={true} at={at} duration={dur}><media file={h} x={0} y={y} width={W} height={2346} fit="fill" animate={a}/></frame>;
}
const Scrim=({at,dur,h=620,top=true})=><rect x={0} y={top?0:H-h} width={W} height={h} at={at} duration={dur} fill={{kind:"linear",angle:top?180:0,stops:[{offset:0,color:NAVY,opacity:0.96},{offset:0.6,color:NAVY,opacity:0.82},{offset:1,color:NAVY,opacity:0}]}}/>;
function BossCard({h,name,x,y,at,dur,rot,w=470,hh=520}){
  return <frame x={x} y={y} width={w} height={hh} layout="none" origin="center" at={at} duration={dur}
    motion={{enter:{from:{scale:1.9,rotation:rot},duration:0.18,easing:{kind:"overshoot",amount:1.3}},exit:{to:{opacity:0},duration:0.1,anchor:"end"}}}>
    <rect x={0} y={0} width={w} height={hh} radius={28} fill={PANEL} strokeColor={GOLD} strokeWidth={8} shadow={{y:14,blur:0,color:OUTLINE}}/>
    <media file={h} x={70} y={46} width={w-140} height={320} fit="contain"/>
    <text x={0} y={392} width={w} height={90} align="center" fontFamily="Jua" fontSize={66} lineHeight={1.3} color={WHITE}>{name}</text>
  </frame>;
}
// 기술 연출 영상(Tools/ad_motion/pack_skill_clips.py) 안의 시작 프레임
const START={hero_swing:0,doc_finish_sentence:28,ara_pebble:69,wand_thunder_spell:99,quill_ink_storm:128,gem_letter_shower:164,doc_highlight_sweep:205,whip_thorn_storm:246,lamp_firefly_swarm:286,leaf_world_breath:328,grail_blessing:358,gourd_spring:387,flute_echo_wall:427,tortoise_encyclo_wall:467,hourglass_rewind:508,seed_period_grove:550};
// 몬스터 줄 또는 파티 줄을 크게 (1.6배). party = 회복·보호막
function Shot({h,clip,from=0,at,dur,party=false}){
  return <frame x={0} y={0} width={W} height={H} layout="none" clip={true} at={at} duration={dur}>
    <media file={h} x={-324} y={party?-1168:216} width={1728} height={3754} fit="fill" trimStart={(START[clip]+from)/30}
      animate={[{property:"scale",keyframes:[{at:0,value:1.07,easing:"ease-out"},{at:Math.min(0.14,dur*0.4),value:1},{at:dur-0.01,value:1.025}]}]}/>
  </frame>;
}
// 음악(battle.ogg 120BPM)에 맞춤: 공격 4분할 2장 → 회복 → 보호막, 각 두 박자. 글은 한 줄만
const BEAT=0.4992,REEL_AT=4.0;
// [기술, 시작 프레임] — 시작 프레임을 달리해 맞는 순간이 왼쪽 위 → 오른쪽 아래로 번짐 (기술은 대개 10번째 프레임에 맞음)
const GRIDS=[[["quill_ink_storm",9],["gem_letter_shower",6],["whip_thorn_storm",3],["ara_pebble",0]],
  [["doc_highlight_sweep",9],["lamp_firefly_swarm",6],["doc_finish_sentence",3],["wand_thunder_spell",0]]];
const SOLO=["gourd_spring","tortoise_encyclo_wall"];
// 4분할 한 칸: 왼쪽 몬스터 카드 쪽을 크게. 영상은 칸마다 따로 (Tools/ad_motion/cut_cells.sh — 같은 파일을 동시에 틀면 같은 프레임이 나옴)
function Cell({h,x,y,at,dur,w=535,hh=590}){
  return <frame x={x} y={y} width={w} height={hh} layout="none" origin="center" clip={true} radius={22} at={at} duration={dur}
    motion={{enter:{from:{scale:0.86},duration:0.16,easing:{kind:"overshoot",amount:1.6}}}}>
    <media file={h} x={-182} y={-135} width={1350} height={2933} fit="fill"/>
    <rect x={0} y={0} width={w} height={hh} radius={22} fill="#FFFFFF00" strokeColor={GOLD} strokeWidth={8}/>
  </frame>;
}
// 제목 한 줄: 박자마다 톡톡 (읽을 글은 이것만)
function BeatHead({s,at,len,color=GOLD,size=140}){
  const keys=[],n=Math.floor(len/BEAT+0.001);
  for(let i=0;i<n;i++){keys.push({at:i*BEAT,value:1.1,easing:"ease-out"},{at:i*BEAT+0.16,value:1});if(i<n-1)keys.push({at:(i+1)*BEAT-0.02,value:1});}
  return <frame x={0} y={130} width={W} height={240} layout="none" origin="center" at={at} duration={len}
    animate={[{property:"scale",keyframes:keys}]}>
    <Stamp s={s} cx={540} cy={120} size={size} color={color} at={0} dur={len-0.02}/>
  </frame>;
}
// 필드 장면: z=1.3이면 주인공이 가운데 위쪽, 위 HUD는 제목 뒤로·스틱은 화면 밖 / z=1이면 선제공격 창이 다 들어옴
function FieldShot({h,at,dur,z=1.3,y=-462,from=0}){
  return <frame x={0} y={0} width={W} height={H} layout="none" clip={true} at={at} duration={dur}>
    <media file={h} x={(W-W*z)/2} y={y} width={W*z} height={2346*z} fit="fill" trimStart={from}
      animate={[{property:"scale",keyframes:[{at:0,value:1.05,easing:"ease-out"},{at:0.14,value:1},{at:dur-0.01,value:1}]}]}/>
  </frame>;
}
export default async ({project})=>{
  const p=await project({size:"1080x1920",fps:30,background:NAVY});
  const A=n=>p.add("assets/"+n);
  const qz=await A("real_quiz.mp4"),fn=await A("real_finisher.mp4"),ch=await A("real_chest.mp4"),reel=await A("skills_reel.mp4"),icon=await A("icon.png");
  const b1=await A("boss_forget_king.png"),b2=await A("boss_muddle_raccoon.png"),b3=await A("boss_blank_frog.png"),b4=await A("boss_ellipsis_vine.png");
  const advRaft=await A("adv_raft.mp4"),advCave=await A("adv_cave.mp4"),advPreempt=await A("adv_preempt.mp4");
  const cells={};
  for(const g of GRIDS)for(const [clip] of g)cells[clip]=await A("cell_"+clip+".mp4");
  const LATE=1.55,LATE2=5.39,T=18+LATE2;
  let kids=[];
  const add=(...n)=>kids.push(...n.flat());
  // 1 문제 도전 0~3 — 실제 전투, 크리티컬 찬스 줄이 타들어 감. 2.21초에 정답 (크리티컬 3초 안), 초록 칸은 게임 화면 그대로
  add(<FieldShot key="g1" h={qz} at={0} dur={3.0} z={1} y={-680}/>,<Scrim key="s1" at={0} dur={3.0} h={560}/>,<Scrim key="s1b" at={0} dur={3.0} top={false} h={330}/>);
  add(<Stamp key="h1" s="3초 안에" cx={540} cy={235} size={150} color={GOLD} at={0.05} dur={2.12}/>,<Stamp key="h2" s="맞혀 보세요!" cx={540} cy={395} size={118} at={0.28} dur={1.89} from={1.8}/>);
  add(<frame key="ring" x={765} y={530} width={210} height={210} layout="none" origin="center" at={0.2} duration={1.97} motion={{enter:{from:{scale:0},duration:0.16,easing:{kind:"overshoot",amount:2}},exit:{to:{scale:0,opacity:0},duration:0.1,anchor:"end"}}}><rect x={0} y={0} width={210} height={210} radius={105} fill={NAVY} strokeColor={GOLD} strokeWidth={12}/></frame>);
  [["3",0.3],["2",0.9],["1",1.5]].forEach(([s,t])=>add(<Stamp key={"d"+s} s={s} cx={870} cy={635} w={210} size={124} color={s==="1"?RED:WHITE} sw={10} at={t} dur={0.62} from={1.7}/>));
  add(<Flash key="f1" at={2.21} o={0.5}/>,...Burst({cx:540,cy:1171,at:2.21,seed:11,colors:[GREEN,WHITE,GOLD]}));
  add(<Stamp key="ok1" s="정답!" cx={540} cy={250} size={190} color={GREEN} at={2.21} dur={0.79}/>,<Stamp key="ok2" s="potential = 잠재력, 가능성" cx={540} cy={430} size={66} at={2.33} dur={0.67} from={1.3}/>);
  add(Wipe({at:2.75,seed:21}));
  // 2 공격 3~4 — 같은 전투의 다음 장면 (실제 크리티컬)
  add(<FieldShot key="g2" h={qz} from={3.6} at={3.0} dur={1.0} z={1.6} y={216}/>,<Scrim key="s2" at={3.0} dur={1.0} h={470}/>,<Scrim key="s2b" at={3.0} dur={1.0} top={false} h={380}/>);
  add(<Stamp key="a1" s="맞히면, 공격!" cx={540} cy={250} size={124} color={GOLD} at={3.05} dur={0.93}/>);
  add(<Flash key="f3" at={3.47} c={GOLD} o={0.45}/>,...Burst({cx:255,cy:960,at:3.47,seed:32,spread:560,colors:[RED,GOLD,WHITE]}));
  add(<Stamp key="a3" s="3초 안에 맞히면" cx={540} cy={1505} size={56} at={3.47} dur={0.5} from={1.2}/>,<Stamp key="a2" s="크리티컬 1.5배!" cx={540} cy={1610} size={110} color={RED} at={3.49} dur={0.48} rot={-6}/>);
  add(Wipe({at:3.66,seed:22}));
  // 2-2 전투 구간 — 공격 4분할 → 회복·보호막 → 공격 4분할, 글은 짧게 나눠서 (사용자: 신나는 전투가 너무 김)
  {
    let c=REEL_AT;
    const grid=(g,n)=>{
      g.forEach(([clip],i)=>add(<Cell key={`q${n}-${i}`} h={cells[clip]} x={i%2?545:0} y={i<2?400:1000} at={c} dur={BEAT*2}/>));
      if(n>0)add(<Flash key={"qf"+n} at={c} o={0.3} d={0.12}/>);
      c+=BEAT*2;
    };
    const solo=(clip,i)=>{
      add(<Shot key={"so"+i} h={reel} clip={clip} from={9} at={c} dur={BEAT*2} party={true}/>);
      add(<rect key={"som"+i} x={0} y={0} width={W} height={668} at={c} duration={BEAT*2} fill={{kind:"linear",angle:180,stops:[{offset:0,color:NAVY,opacity:1},{offset:0.92,color:NAVY,opacity:1},{offset:1,color:NAVY,opacity:0}]}}/>);
      add(<Flash key={"sof"+i} at={c} o={0.3} d={0.12}/>);
      c+=BEAT*2;
    };
    grid(GRIDS[0],0);
    const h1=c;
    solo(SOLO[0],0); solo(SOLO[1],1);
    const h2=c;
    grid(GRIDS[1],1);
    add(<BeatHead key="rh0" s="신나는 전투!" at={REEL_AT} len={h1-REEL_AT}/>,<BeatHead key="rh1" s="다양한 스킬로" at={h1} len={h2-h1} size={124}/>,<BeatHead key="rh2" s="적을 무찔러보세요!" at={h2} len={c-h2} size={104}/>);
    add(Wipe({at:c-0.22,seed:26}));
  }
  const early=kids; kids=[];
  // 3 결정타 — 실제 보스전 (7 연속 정답 → 쓰러뜨림). 묶음 시간 7.45 + LATE = 9.0 = 음악 박자, 쿵 소리는 여기만
  add(<FieldShot key="g3" h={fn} from={1.866} at={6.45} dur={BEAT*6} z={1} y={40}/>,<Scrim key="s3" at={6.45} dur={BEAT*6} h={300}/>);
  add(<Stamp key="c0" s="맞힐수록 더 세게!" cx={540} cy={215} size={106} at={6.5} dur={0.9} color={WHITE}/>);
  add(...Burst({cx:540,cy:560,at:7.448,seed:41,n:44,spread:760,colors:[GOLD,RED,WHITE]}));
  add(Wipe({at:6.45+BEAT*6-0.22,seed:23}));
  // 4 모험 — 실제 게임 화면 (사용자: 숲 풀숲을 걷다가 상자, 뗏목, 선제공격). 묶음 시간 9.45 = 음악 박자 위
  {
    const A0=9.45; let c=A0;
    add(<FieldShot key="adv1" h={ch} from={0.24} at={c} dur={BEAT*5}/>);
    add(...Burst({cx:769,cy:750,at:c+1.483,seed:51,n:30,spread:420,colors:[GOLD,WHITE]})); // 상자가 열리는 순간 (음악 박자 위)
    const c1=c+BEAT*5;
    add(<Flash key="adf1" at={c1} o={0.3} d={0.12}/>,<FieldShot key="adv2" h={advRaft} at={c1} dur={BEAT*3}/>);
    const c2=c1+BEAT*3;
    add(<Flash key="adf2" at={c2} o={0.3} d={0.12}/>,<FieldShot key="adv3" h={advCave} at={c2} dur={BEAT*2}/>);
    const c3=c2+BEAT*2;
    add(<rect key="adsc" x={0} y={0} width={W} height={470} at={A0} duration={c3-A0} fill={{kind:"linear",angle:180,stops:[{offset:0,color:NAVY,opacity:0.98},{offset:0.88,color:NAVY,opacity:0.98},{offset:1,color:NAVY,opacity:0}]}}/>);
    const MINT="#7BF29A";
    add(<BeatHead key="adh0" s="신나는 모험!" at={A0} len={c1-A0} color={MINT}/>,<BeatHead key="adh1" s="강을 건너고" at={c1} len={c2-c1} color={MINT} size={124}/>,<BeatHead key="adh2" s="어둠을 밝혀요" at={c2} len={c3-c2} color={MINT} size={124}/>);
    add(<Flash key="adf3" at={c3} o={0.3} d={0.12}/>,<FieldShot key="adv4" h={advPreempt} at={c3} dur={BEAT*3} z={1} y={-250}/>);
    add(Wipe({at:c3+BEAT*3-0.22,seed:24}));
  }
  const mid=kids; kids=[];
  // 5 보스 12.1~14.5
  add(<rect key="bg5" x={0} y={0} width={W} height={H} fill={{kind:"radial",stops:[{offset:0,color:"#2B3158"},{offset:1,color:NAVY}]}} at={12.1} duration={2.55}/>);
  add(<Stamp key="b0" s="단어를 노리는 보스들" cx={540} cy={330} size={98} color={GOLD} at={12.15} dur={2.45}/>);
  add(<BossCard key="b1" h={b1} name="까먹대왕" x={50} y={470} at={12.6} dur={2.0} rot={-8}/>,<BossCard key="b2" h={b2} name="헷갈너구리" x={560} y={470} at={12.85} dur={1.75} rot={8}/>,<BossCard key="b3" h={b3} name="빈칸두꺼비" x={50} y={1020} at={13.1} dur={1.5} rot={8}/>,<BossCard key="b4" h={b4} name="말줄임덩굴" x={560} y={1020} at={13.35} dur={1.25} rot={-8}/>);
  add(Wipe({at:14.42,seed:25}));
  // 6 엔드 카드 14.65~18
  const r=rng(99);
  for(let i=0;i<34;i++){const c="ABCDEFGHIJKLMNOPQRSTUVWXYZ"[Math.floor(r()*26)],x=r()*1000,y=r()*1900,s=60+r()*110,dy=120+r()*220;
    add(<text key={"l"+i} x={x} y={y} width={200} height={s*1.4} fontFamily="Jua" fontSize={s} color="#2A3056" at={14.65} duration={3.34} animate={[{property:"offsetY",from:0,to:-dy,duration:3.3,easing:"linear"}]}>{c}</text>);}
  add(<frame key="ic" x={350} y={330} width={380} height={380} layout="none" origin="center" at={14.75} duration={3.24} motion={{enter:{from:{scale:0,rotation:-14},duration:0.28,easing:{kind:"overshoot",amount:2}}}}><rect x={-10} y={-10} width={400} height={400} radius={94} fill={GOLD}/><media file={icon} x={0} y={0} width={380} height={380} fit="fill" radius={84}/></frame>);
  add(...Burst({cx:540,cy:520,at:14.85,seed:61,n:36,spread:640}));
  add(<Stamp key="e1" s="영단어RPG" cx={540} cy={850} size={168} color={GOLD} at={15.0} dur={2.99} from={1.8}/>);
  add(<Stamp key="e2" s="게임하다 보니 외워지는 영단어" cx={540} cy={1020} size={66} sw={10} at={15.25} dur={2.74} from={1.25}/>);
  add(<Stamp key="e3" s="교육과정 필수 어휘로 싸우는 RPG" cx={540} cy={1105} size={46} color={DIM} sw={8} at={15.4} dur={2.59} from={1.1}/>);
  add(<Pill key="e4" s="앱스토어에서 무료로 시작" cy={1265} at={15.6} dur={2.39} w={860} h={132} size={66} from={1200}/>);
  add(<Stamp key="e5" s="숲 지역부터 정식판 (앱 내 구매)" cx={540} cy={1395} size={38} color={DIM} sw={6} at={15.8} dur={2.19} from={1}/>);
  p.compose(<frame width={W} height={H} layout="none">{early}<frame x={0} y={0} width={W} height={H} layout="none" at={LATE} duration={16.3}>{mid}</frame><frame x={0} y={0} width={W} height={H} layout="none" at={LATE2} duration={18}>{kids}</frame></frame>,{at:0,dur:T,name:"ad"});
};
